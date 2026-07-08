using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageTimelineView : VisualElement
    {
        private const float RulerHeight = 22f;
        private const float TrackHeight = 40f;
        private const float TrackGap = 8f;
        private const float TrackLabelWidth = 56f;
        private const float ContentPadding = 8f;
        private const float SnapStep = 0.01f;
        private const float EdgeHandleWidth = 6f;
        private const float MagneticSnapPixels = 10f;
        private const float MinSegmentDuration = 0.05f;
        private const float DefaultQuickBlendDuration = 0.2f;

        private static readonly Color SegmentCoreColor = new(0.28f, 0.52f, 0.92f, 0.92f);
        private static readonly Color SegmentSelectedColor = new(0.38f, 0.64f, 1f, 0.98f);
        private static readonly Color AutoBlendOverlayColor = new(0.95f, 0.52f, 0.18f, 0.48f);
        private static readonly Color TrackRowColor = new(0.1f, 0.1f, 0.11f, 1f);

        private enum DragMode
        {
            None,
            Playhead,
            SegmentMove,
            SegmentTrimStart,
            SegmentTrimEnd,
            TimelinePan,
            TrackReorder,
            NotifyMove,
            NotifyStateMove,
            NotifyStateResizeStart,
            NotifyStateResizeEnd,
            BoxSelect
        }

        private enum PendingCreateKind
        {
            Segment,
            Notify,
            NotifyState,
            ReplaceSegmentClip,
            ReplaceNotify,
            ReplaceNotifyState
        }

        private enum TrackKind
        {
            Segment,
            Notify,
            NotifyState
        }

        private readonly struct TrackRowLayout
        {
            public TrackRowLayout(TrackKind kind, string trackId, Rect rect)
            {
                Kind = kind;
                TrackId = trackId;
                Rect = rect;
            }

            public TrackKind Kind { get; }
            public string TrackId { get; }
            public Rect Rect { get; }
        }

        private readonly struct TrackIdentity
        {
            public TrackIdentity(TrackKind kind, string trackId)
            {
                Kind = kind;
                TrackId = trackId;
            }

            public TrackKind Kind { get; }
            public string TrackId { get; }
        }

        private readonly struct SegmentLayout
        {
            public SegmentLayout(int index, Rect body)
            {
                Index = index;
                Body = body;
            }

            public int Index { get; }
            public Rect Body { get; }
        }

        private readonly struct NotifyStateLayout
        {
            public NotifyStateLayout(int index, Rect body)
            {
                Index = index;
                Body = body;
            }

            public int Index { get; }
            public Rect Body { get; }
        }

        private readonly struct NotifyLayout
        {
            public NotifyLayout(int index, Rect body)
            {
                Index = index;
                Body = body;
            }

            public int Index { get; }
            public Rect Body { get; }
        }

        private readonly struct HoverTooltipInfo
        {
            public HoverTooltipInfo(string text, float minWidth = 96f)
            {
                Text = text;
                MinWidth = minWidth;
            }

            public string Text { get; }
            public float MinWidth { get; }
        }

        private sealed class ObjectPickerPopup : EditorWindow
        {
            private readonly List<UnityEngine.Object> assets = new();
            private Type objectType;
            private Action<UnityEngine.Object> onPick;
            private string searchText = string.Empty;
            private Vector2 scroll;
            private bool consumed;
            private string actionLabel = "Select";
            private UnityEngine.Object selectedAsset;
            private UnityEngine.Object previewAsset;
            private Editor previewEditor;

            public static void Show<T>(string title, Action<T> onPick) where T : UnityEngine.Object
            {
                ObjectPickerPopup window = CreateInstance<ObjectPickerPopup>();
                window.titleContent = new GUIContent(title);
                window.minSize = new Vector2(340f, 420f);
                window.position = new Rect(160f, 160f, 380f, 460f);
                window.actionLabel = title.StartsWith("Replace", StringComparison.OrdinalIgnoreCase) ? "Replace" : "Create";
                window.Initialize(typeof(T), picked =>
                {
                    if (picked is T typed)
                        onPick?.Invoke(typed);
                });
                window.ShowAuxWindow();
                window.Focus();
            }

            private void Initialize(Type type, Action<UnityEngine.Object> pickCallback)
            {
                objectType = type;
                onPick = pickCallback;
                assets.Clear();

                string[] guids = AssetDatabase.FindAssets($"t:{objectType.Name}");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    UnityEngine.Object[] loadedAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int j = 0; j < loadedAssets.Length; j++)
                    {
                        UnityEngine.Object asset = loadedAssets[j];
                        if (asset == null || !objectType.IsInstanceOfType(asset) || IsInternalPreviewAsset(asset))
                            continue;

                        if (!assets.Contains(asset))
                            assets.Add(asset);
                    }
                }

                assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            }

            private void OnGUI()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.5f)))
                    {
                        searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
                        scroll = EditorGUILayout.BeginScrollView(scroll);
                        for (int i = 0; i < assets.Count; i++)
                        {
                            UnityEngine.Object asset = assets[i];
                            if (asset == null || !MatchesSearch(asset))
                                continue;

                            GUIContent content = new(asset.name, AssetDatabase.GetAssetPath(asset));
                            Rect rowRect = GUILayoutUtility.GetRect(content, EditorStyles.objectField, GUILayout.Height(20f));
                            bool selected = selectedAsset == asset;
                            if (selected && Event.current.type == EventType.Repaint)
                                EditorGUI.DrawRect(rowRect, new Color(0.28f, 0.48f, 0.78f, 0.38f));

                            if (GUI.Button(rowRect, content, EditorStyles.objectField))
                                SelectAsset(asset);

                            if (selected && Event.current.type == EventType.Repaint)
                                GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);
                        }

                        EditorGUILayout.EndScrollView();
                    }

                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawAssetPreview();
                    }
                }
            }

            private bool MatchesSearch(UnityEngine.Object asset) =>
                string.IsNullOrWhiteSpace(searchText)
                || asset.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

            private static bool IsInternalPreviewAsset(UnityEngine.Object asset) =>
                asset.name.IndexOf("__preview__", StringComparison.OrdinalIgnoreCase) >= 0;

            private void SelectAsset(UnityEngine.Object asset)
            {
                selectedAsset = asset;
                SetPreviewAsset(asset);
            }

            private void SetPreviewAsset(UnityEngine.Object asset)
            {
                if (previewAsset == asset)
                    return;

                DestroyPreviewEditor();
                previewAsset = asset;
                if (previewAsset != null)
                    previewEditor = Editor.CreateEditor(previewAsset);
            }

            private void DrawAssetPreview()
            {
                GUILayout.Label(previewAsset != null ? previewAsset.name : "Preview", EditorStyles.boldLabel);
                Rect previewRect = GUILayoutUtility.GetRect(160f, Mathf.Max(120f, position.height - 112f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (previewEditor != null && previewEditor.HasPreviewGUI())
                {
                    previewEditor.OnInteractivePreviewGUI(previewRect, GUIStyle.none);
                    Repaint();
                }
                else
                {
                    Texture2D preview = previewAsset != null ? AssetPreview.GetAssetPreview(previewAsset) : null;
                    if (preview == null && previewAsset != null)
                        preview = AssetPreview.GetMiniThumbnail(previewAsset);

                    if (preview != null)
                        GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
                    else
                        EditorGUI.HelpBox(previewRect, "Select an asset to preview it.", MessageType.Info);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(selectedAsset == null || consumed))
                    {
                        if (GUILayout.Button(actionLabel, GUILayout.Width(96f), GUILayout.Height(24f)))
                            Pick(selectedAsset);
                    }
                }
            }

            private void Pick(UnityEngine.Object asset)
            {
                if (consumed)
                    return;

                consumed = true;
                onPick?.Invoke(asset);
                Close();
            }

            private void OnDisable()
            {
                DestroyPreviewEditor();
            }

            private void DestroyPreviewEditor()
            {
                if (previewEditor == null)
                    return;

                DestroyImmediate(previewEditor);
                previewEditor = null;
            }
        }

        private readonly MontageEditorContext context;
        private readonly List<SegmentLayout> segmentLayouts = new();
        private readonly List<NotifyLayout> notifyLayouts = new();
        private readonly List<NotifyStateLayout> notifyStateLayouts = new();
        private readonly List<TrackRowLayout> trackRows = new();
        private readonly Dictionary<int, float> dragSegmentStartTimes = new();
        private readonly Dictionary<int, float> dragNotifyTimes = new();
        private readonly Dictionary<int, Vector2> dragNotifyStateRanges = new();
        private readonly Label hoverTooltip;

        private float pixelsPerSecond = 120f;
        private float viewStartTime;
        private float viewStartY;
        private float totalTrackContentHeight;

        private DragMode dragMode = DragMode.None;
        private int dragSegmentIndex = -1;
        private int dragNotifyIndex = -1;
        private int dragNotifyStateIndex = -1;
        private TrackKind dragTrackKind;
        private string dragTrackId = "Default";
        private float dragAnchorTime;
        private float dragAnchorValue;
        private float dragAnchorClipStart;
        private float dragAnchorClipEnd;
        private float dragAnchorSegmentEnd;
        private float dragAnchorY;
        private float dragAnchorScrollY;
        private float dragNotifyStateDuration;
        private Vector2 boxSelectStart;
        private Vector2 boxSelectEnd;
        private bool boxSelectAdditive;
        private bool hasSnapGuide;
        private float snapGuideTime;
        private bool hasHoverTrack;
        private TrackKind hoverTrackKind;
        private string hoverTrackId = "Default";

        public MontageTimelineView(MontageEditorContext context)
        {
            this.context = context;
            AddToClassList(AnimMontageEditorStyles.TimelineHostClass);
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.flexBasis = 0;
            style.minHeight = 160;
            style.overflow = Overflow.Hidden;
            focusable = true;

            hoverTooltip = new Label { pickingMode = PickingMode.Ignore };
            hoverTooltip.style.position = Position.Absolute;
            hoverTooltip.style.display = DisplayStyle.None;
            hoverTooltip.style.paddingLeft = 8f;
            hoverTooltip.style.paddingRight = 8f;
            hoverTooltip.style.paddingTop = 4f;
            hoverTooltip.style.paddingBottom = 4f;
            hoverTooltip.style.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.94f);
            hoverTooltip.style.borderTopLeftRadius = 3f;
            hoverTooltip.style.borderTopRightRadius = 3f;
            hoverTooltip.style.borderBottomLeftRadius = 3f;
            hoverTooltip.style.borderBottomRightRadius = 3f;
            hoverTooltip.style.color = new Color(1f, 1f, 1f, 0.95f);
            hoverTooltip.style.fontSize = 11f;
            Add(hoverTooltip);

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Undo.undoRedoPerformed += OnUndoRedoPerformed;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            });

            context.Changed += RequestRepaint;
            context.PlayheadChanged += RequestRepaint;
        }

        private void RequestRepaint() => MarkDirtyRepaint();

        private void OnUndoRedoPerformed()
        {
            context.NotifyExternalChange();
            MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent evt)
        {
            float cursorTime = XToTime(evt.localMousePosition.x);
            float oldPixelsPerSecond = pixelsPerSecond;
            pixelsPerSecond = Mathf.Clamp(pixelsPerSecond - evt.delta.y * 4f, 40f, 400f);

            if (!Mathf.Approximately(oldPixelsPerSecond, pixelsPerSecond))
            {
                float contentX = evt.localMousePosition.x - TrackLabelWidth - ContentPadding;
                viewStartTime = cursorTime - Mathf.Max(0f, contentX) / pixelsPerSecond;
            }

            ClampViewStartTime();
            evt.StopPropagation();
            MarkDirtyRepaint();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.target is TextField)
                return;

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            if (!DeleteHoveredTrack() && !DeleteSelected())
                return;

            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (context.Montage == null)
                return;

            focusController?.IgnoreEvent(evt);
            Focus();
            Vector2 local = evt.localPosition;

            if (evt.button == 2)
            {
                BeginDrag(DragMode.TimelinePan, evt.pointerId);
                dragAnchorTime = local.x;
                dragAnchorValue = viewStartTime;
                dragAnchorY = local.y;
                dragAnchorScrollY = viewStartY;
                evt.StopPropagation();
                return;
            }

            if (evt.button == 1)
            {
                ShowContextMenu(local);
                evt.StopPropagation();
                return;
            }

            if (TryGetTrackRow(local, out TrackRowLayout row) && IsTrackLabel(local, row))
            {
                bool additive = evt.shiftKey;
                bool toggle = evt.ctrlKey || evt.commandKey;
                string trackKey = GetTrackKey(row.Kind, row.TrackId);
                context.SetSelectedTimelineTrack(trackKey, additive || toggle, toggle);
                if (additive || toggle)
                {
                    evt.StopPropagation();
                    return;
                }

                BeginDrag(DragMode.TrackReorder, evt.pointerId);
                dragTrackKind = row.Kind;
                dragTrackId = row.TrackId;
                evt.StopPropagation();
                return;
            }

            if (TryHitPlayhead(local, out _))
            {
                BeginDrag(DragMode.Playhead, evt.pointerId);
                SetPlayheadFromX(local.x);
                evt.StopPropagation();
                return;
            }

            if (TryHitSegment(local, out dragSegmentIndex, out DragMode segmentDrag))
            {
                bool additive = evt.shiftKey;
                bool toggle = evt.ctrlKey || evt.commandKey;
                if (additive || toggle || !context.IsSegmentSelected(dragSegmentIndex))
                    context.SetSelectedSegment(dragSegmentIndex, additive, toggle);
                if (toggle && !context.IsSegmentSelected(dragSegmentIndex))
                {
                    evt.StopPropagation();
                    return;
                }

                BeginDrag(segmentDrag, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                dragAnchorValue = GetSegmentDragValue(segmentDrag, dragSegmentIndex);
                MontageSegment segment = context.Montage.Segments[dragSegmentIndex];
                dragAnchorClipStart = segment.ClipStartTime;
                dragAnchorClipEnd = segment.ClipEndTime;
                dragAnchorSegmentEnd = segment.EndTime;
                CaptureDragSelectionAnchors();
                evt.StopPropagation();
                return;
            }

            if (TryHitNotifyState(local, out dragNotifyStateIndex, out DragMode stateDrag))
            {
                bool additive = evt.shiftKey;
                bool toggle = evt.ctrlKey || evt.commandKey;
                if (additive || toggle || !context.IsNotifyStateSelected(dragNotifyStateIndex))
                    context.SetSelectedNotifyState(dragNotifyStateIndex, additive, toggle);
                if (toggle && !context.IsNotifyStateSelected(dragNotifyStateIndex))
                {
                    evt.StopPropagation();
                    return;
                }

                BeginDrag(stateDrag, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                AnimNotifyStatePlacement placement = context.Montage.NotifyStates[dragNotifyStateIndex];
                dragNotifyStateDuration = placement.Duration;
                dragAnchorValue = stateDrag == DragMode.NotifyStateResizeEnd
                    ? placement.EndTime
                    : placement.StartTime;
                CaptureDragSelectionAnchors();
                evt.StopPropagation();
                return;
            }

            if (TryHitNotify(local, out dragNotifyIndex))
            {
                bool additive = evt.shiftKey;
                bool toggle = evt.ctrlKey || evt.commandKey;
                if (additive || toggle || !context.IsNotifySelected(dragNotifyIndex))
                    context.SetSelectedNotify(dragNotifyIndex, additive, toggle);
                if (toggle && !context.IsNotifySelected(dragNotifyIndex))
                {
                    evt.StopPropagation();
                    return;
                }

                BeginDrag(DragMode.NotifyMove, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                dragAnchorValue = context.Montage.Notifies[dragNotifyIndex].Time;
                CaptureDragSelectionAnchors();
                evt.StopPropagation();
                return;
            }

            if (evt.clickCount == 2 && TryGetTrackRow(local, TrackKind.Notify, out TrackRowLayout notifyRow))
            {
                AddNotifyAtTime(XToTime(local.x), null, notifyRow.TrackId);
                evt.StopPropagation();
                return;
            }

            if (local.y <= RulerHeight)
            {
                BeginDrag(DragMode.Playhead, evt.pointerId);
                SetPlayheadFromX(local.x);
                evt.StopPropagation();
                return;
            }

            if (TryGetTrackRow(local, out _))
            {
                BeginDrag(DragMode.BoxSelect, evt.pointerId);
                boxSelectStart = local;
                boxSelectEnd = local;
                boxSelectAdditive = evt.shiftKey || evt.ctrlKey || evt.commandKey;
                HideHoverTooltip();
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
            {
                UpdateHoverTrack(evt.localPosition);
                return;
            }

            float time = XToTime(evt.localPosition.x);
            hasSnapGuide = false;
            switch (dragMode)
            {
                case DragMode.Playhead:
                    context.SetPlayhead(Snap(time));
                    break;

                case DragMode.NotifyMove when dragNotifyIndex >= 0:
                    ApplySelectedTimelineMove(Snap(time) - dragAnchorValue, evt.localPosition, TrackKind.Notify);
                    break;

                case DragMode.SegmentMove when dragSegmentIndex >= 0:
                    float segmentStartTime = SnapSegmentStartTime(dragSegmentIndex, Mathf.Max(0f, time - dragAnchorTime + dragAnchorValue));
                    ApplySelectedTimelineMove(segmentStartTime - dragAnchorValue, evt.localPosition, TrackKind.Segment);
                    break;

                case DragMode.SegmentTrimStart when dragSegmentIndex >= 0:
                    ApplySegmentTrimStart(dragSegmentIndex, time);
                    break;

                case DragMode.SegmentTrimEnd when dragSegmentIndex >= 0:
                    ApplySegmentTrimEnd(dragSegmentIndex, time);
                    break;

                case DragMode.TimelinePan:
                    viewStartTime = dragAnchorValue - (evt.localPosition.x - dragAnchorTime) / Mathf.Max(1f, pixelsPerSecond);
                    viewStartY = dragAnchorScrollY - (evt.localPosition.y - dragAnchorY);
                    ClampViewStartTime();
                    ClampViewStartY();
                    MarkDirtyRepaint();
                    break;

                case DragMode.TrackReorder:
                    ApplyTrackReorder(evt.localPosition);
                    break;

                case DragMode.BoxSelect:
                    boxSelectEnd = evt.localPosition;
                    MarkDirtyRepaint();
                    break;

                case DragMode.NotifyStateMove when dragNotifyStateIndex >= 0:
                    ApplySelectedTimelineMove(time - dragAnchorTime, evt.localPosition, TrackKind.NotifyState);
                    break;

                case DragMode.NotifyStateResizeStart when dragNotifyStateIndex >= 0:
                    ApplyNotifyStateRange(
                        dragNotifyStateIndex,
                        time,
                        context.Montage.NotifyStates[dragNotifyStateIndex].EndTime);
                    break;

                case DragMode.NotifyStateResizeEnd when dragNotifyStateIndex >= 0:
                    ApplyNotifyStateRange(
                        dragNotifyStateIndex,
                        context.Montage.NotifyStates[dragNotifyStateIndex].StartTime,
                        time);
                    break;
            }

            evt.StopPropagation();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (dragMode != DragMode.None)
                return;

            hasHoverTrack = false;
            HideHoverTooltip();
            MarkDirtyRepaint();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;

            this.ReleasePointer(evt.pointerId);
            DragMode endedDragMode = dragMode;
            dragMode = DragMode.None;
            dragSegmentIndex = -1;
            dragNotifyIndex = -1;
            dragNotifyStateIndex = -1;
            dragTrackId = "Default";
            hasSnapGuide = false;
            if (endedDragMode == DragMode.BoxSelect)
                ApplyBoxSelection();

            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void BeginDrag(DragMode mode, int pointerId)
        {
            dragMode = mode;
            this.CapturePointer(pointerId);
        }

        private void CaptureDragSelectionAnchors()
        {
            dragSegmentStartTimes.Clear();
            dragNotifyTimes.Clear();
            dragNotifyStateRanges.Clear();

            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            foreach (int index in context.SelectedSegmentIndices)
            {
                if (index >= 0 && index < montage.Segments.Count && montage.Segments[index] != null)
                    dragSegmentStartTimes[index] = montage.Segments[index].StartTime;
            }

            foreach (int index in context.SelectedNotifyIndices)
            {
                if (index >= 0 && index < montage.Notifies.Count && montage.Notifies[index] != null)
                    dragNotifyTimes[index] = montage.Notifies[index].Time;
            }

            foreach (int index in context.SelectedNotifyStateIndices)
            {
                if (index < 0 || index >= montage.NotifyStates.Count || montage.NotifyStates[index] == null)
                    continue;

                AnimNotifyStatePlacement state = montage.NotifyStates[index];
                dragNotifyStateRanges[index] = new Vector2(state.StartTime, state.EndTime);
            }
        }

        private void ShowContextMenu(Vector2 local)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            float time = Snap(XToTime(local.x));
            GenericMenu menu = new();
            bool hasRow = TryGetTrackRow(local, out TrackRowLayout row);

            if (TryHitSegment(local, out int segmentIndex, out _))
            {
                menu.AddItem(new GUIContent("Segment/Replace Clip..."), false, () => OpenCreatePicker(PendingCreateKind.ReplaceSegmentClip, time, "Default", segmentIndex));
                if (Selection.activeObject is AnimationClip replacementClip)
                    menu.AddItem(new GUIContent("Segment/Replace Clip From Project Selection"), false, () => ReplaceSegmentClip(segmentIndex, replacementClip));
                else
                    menu.AddDisabledItem(new GUIContent("Segment/Replace Clip From Project Selection"));
                menu.AddItem(new GUIContent("Segment/Reset Trim"), false, () => ResetSegmentTrim(segmentIndex));
                menu.AddItem(new GUIContent("Segment/Delete"), false, () => DeleteArrayElement("segments", segmentIndex, "Delete Montage Segment"));
                menu.AddItem(new GUIContent("Segment/Select"), false, () => context.SetSelectedSegment(segmentIndex));
                menu.AddSeparator("");
            }

            if (TryHitNotify(local, out int notifyIndex))
            {
                menu.AddItem(new GUIContent("Notify/Replace Notify..."), false, () => OpenCreatePicker(PendingCreateKind.ReplaceNotify, time, "Default", notifyIndex));
                if (Selection.activeObject is AnimNotifySO replacementNotify)
                    menu.AddItem(new GUIContent("Notify/Replace Notify From Project Selection"), false, () => ReplaceNotify(notifyIndex, replacementNotify));
                else
                    menu.AddDisabledItem(new GUIContent("Notify/Replace Notify From Project Selection"));
                menu.AddItem(new GUIContent("Notify/Delete"), false, () => DeleteArrayElement("notifies", notifyIndex, "Delete Anim Notify"));
                menu.AddSeparator("");
            }

            if (TryHitNotifyState(local, out int notifyStateIndex, out _))
            {
                menu.AddItem(new GUIContent("Notify State/Replace Notify State..."), false, () => OpenCreatePicker(PendingCreateKind.ReplaceNotifyState, time, "Default", notifyStateIndex));
                if (Selection.activeObject is AnimNotifyStateSO replacementState)
                    menu.AddItem(new GUIContent("Notify State/Replace Notify State From Project Selection"), false, () => ReplaceNotifyState(notifyStateIndex, replacementState));
                else
                    menu.AddDisabledItem(new GUIContent("Notify State/Replace Notify State From Project Selection"));
                menu.AddItem(new GUIContent("Notify State/Delete"), false, () => DeleteArrayElement("notifyStates", notifyStateIndex, "Delete Anim Notify State"));
                menu.AddSeparator("");
            }

            if (hasRow && row.Kind == TrackKind.Segment)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Animation Segment..."), false, () => OpenCreatePicker(PendingCreateKind.Segment, time, trackId));
                if (Selection.activeObject is AnimationClip selectedClip)
                    menu.AddItem(new GUIContent("Create/Segment From Project Selection"), false, () => AddSegmentAtTime(time, selectedClip, trackId));
                else
                    menu.AddDisabledItem(new GUIContent("Create/Segment From Project Selection"));
            }
            else if (hasRow && row.Kind == TrackKind.Notify)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Notify..."), false, () => OpenCreatePicker(PendingCreateKind.Notify, time, trackId));
                if (Selection.activeObject is AnimNotifySO selectedNotify)
                    menu.AddItem(new GUIContent("Create/Notify From Project Selection"), false, () => AddNotifyAtTime(time, selectedNotify, trackId));
                else
                    menu.AddDisabledItem(new GUIContent("Create/Notify From Project Selection"));
            }
            else if (hasRow && row.Kind == TrackKind.NotifyState)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Notify State..."), false, () => OpenCreatePicker(PendingCreateKind.NotifyState, time, trackId));
                if (Selection.activeObject is AnimNotifyStateSO selectedState)
                    menu.AddItem(new GUIContent("Create/Notify State From Project Selection"), false, () => AddNotifyStateAtTime(time, selectedState, trackId));
                else
                    menu.AddDisabledItem(new GUIContent("Create/Notify State From Project Selection"));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Create"));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Track/Add Animation Track"), false, () => AddTrack("animationTracks", "Animation"));
            menu.AddItem(new GUIContent("Track/Add Notify Track"), false, () => AddTrack("notifyTracks", "Notify"));
            menu.AddItem(new GUIContent("Track/Add Notify State Track"), false, () => AddTrack("notifyStateTracks", "Notify State"));
            if (hasRow && !string.IsNullOrEmpty(row.TrackId) && row.TrackId != "Default")
            {
                string propertyName = GetTrackPropertyName(row.Kind);
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Track/Delete Current Track"), false, () => DeleteTrack(row.Kind, propertyName, trackId));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Track/Delete Current Track"));
            }

            menu.ShowAsContext();
        }

        private bool IsSegmentTrack(Vector2 local) =>
            TryGetTrackRow(local, TrackKind.Segment, out _);

        private bool IsNotifyTrack(Vector2 local) =>
            TryGetTrackRow(local, TrackKind.Notify, out _);

        private bool IsNotifyStateTrack(Vector2 local) =>
            TryGetTrackRow(local, TrackKind.NotifyState, out _);

        private void OpenCreatePicker(PendingCreateKind kind, float time, string trackId, int editIndex = -1)
        {
            trackId = SanitizeTrackId(trackId);

            switch (kind)
            {
                case PendingCreateKind.Segment:
                    ObjectPickerPopup.Show<AnimationClip>("Create Animation Segment", clip => AddSegmentAtTime(time, clip, trackId));
                    break;

                case PendingCreateKind.ReplaceSegmentClip:
                    ObjectPickerPopup.Show<AnimationClip>("Replace Animation Clip", clip => ReplaceSegmentClip(editIndex, clip));
                    break;

                case PendingCreateKind.Notify:
                    ObjectPickerPopup.Show<AnimNotifySO>("Create Notify", notify => AddNotifyAtTime(time, notify, trackId));
                    break;

                case PendingCreateKind.ReplaceNotify:
                    ObjectPickerPopup.Show<AnimNotifySO>("Replace Notify", notify => ReplaceNotify(editIndex, notify));
                    break;

                case PendingCreateKind.NotifyState:
                    ObjectPickerPopup.Show<AnimNotifyStateSO>("Create Notify State", state => AddNotifyStateAtTime(time, state, trackId));
                    break;

                case PendingCreateKind.ReplaceNotifyState:
                    ObjectPickerPopup.Show<AnimNotifyStateSO>("Replace Notify State", state => ReplaceNotifyState(editIndex, state));
                    break;
            }
        }

        private float GetSegmentDragValue(DragMode mode, int segmentIndex)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            return mode switch
            {
                DragMode.SegmentMove => segment.StartTime,
                DragMode.SegmentTrimStart => segment.StartTime,
                DragMode.SegmentTrimEnd => segment.EndTime,
                _ => 0f
            };
        }

        private void ApplySegmentStartTime(int segmentIndex, float startTime)
        {
            startTime = SnapSegmentStartTime(segmentIndex, Mathf.Max(0f, startTime));

            Undo.RecordObject(context.Montage, "Move Montage Segment");
            SerializedObject so = new(context.Montage);
            SerializedProperty segmentProperty = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("startTime").floatValue = startTime;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplySelectedTimelineMove(float deltaTime, Vector2 local, TrackKind activeKind)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            string targetTrackId = null;
            if (TryGetTrackRow(local, activeKind, out TrackRowLayout targetRow))
                targetTrackId = targetRow.TrackId;

            Undo.RecordObject(montage, "Move Montage Timeline Elements");
            SerializedObject so = new(montage);

            SerializedProperty segments = so.FindProperty("segments");
            foreach (KeyValuePair<int, float> entry in dragSegmentStartTimes)
            {
                if (segments == null || entry.Key < 0 || entry.Key >= segments.arraySize)
                    continue;

                SerializedProperty segment = segments.GetArrayElementAtIndex(entry.Key);
                segment.FindPropertyRelative("startTime").floatValue = Snap(Mathf.Max(0f, entry.Value + deltaTime));
                if (activeKind == TrackKind.Segment && targetTrackId != null)
                    segment.FindPropertyRelative("trackId").stringValue = targetTrackId;
            }

            SerializedProperty notifies = so.FindProperty("notifies");
            foreach (KeyValuePair<int, float> entry in dragNotifyTimes)
            {
                if (notifies == null || entry.Key < 0 || entry.Key >= notifies.arraySize)
                    continue;

                SerializedProperty notify = notifies.GetArrayElementAtIndex(entry.Key);
                notify.FindPropertyRelative("time").floatValue = Snap(Mathf.Max(0f, entry.Value + deltaTime));
                if (activeKind == TrackKind.Notify && targetTrackId != null)
                    notify.FindPropertyRelative("trackId").stringValue = targetTrackId;
            }

            SerializedProperty notifyStates = so.FindProperty("notifyStates");
            foreach (KeyValuePair<int, Vector2> entry in dragNotifyStateRanges)
            {
                if (notifyStates == null || entry.Key < 0 || entry.Key >= notifyStates.arraySize)
                    continue;

                float duration = Mathf.Max(MinSegmentDuration, entry.Value.y - entry.Value.x);
                float startTime = Snap(Mathf.Max(0f, entry.Value.x + deltaTime));
                float endTime = Snap(startTime + duration);
                SerializedProperty state = notifyStates.GetArrayElementAtIndex(entry.Key);
                state.FindPropertyRelative("startTime").floatValue = startTime;
                state.FindPropertyRelative("endTime").floatValue = endTime;
                if (activeKind == TrackKind.NotifyState && targetTrackId != null)
                    state.FindPropertyRelative("trackId").stringValue = targetTrackId;
            }

            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplySegmentTrimStart(int segmentIndex, float startTime)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || segmentIndex < 0 || segmentIndex >= montage.Segments.Count)
                return;

            MontageSegment segment = montage.Segments[segmentIndex];
            if (segment?.Clip == null)
                return;

            float playRate = segment.PlayRate;
            float maxStartTime = dragAnchorSegmentEnd - MinSegmentDuration;
            startTime = Snap(Mathf.Clamp(startTime, 0f, maxStartTime));
            float deltaClip = (startTime - dragAnchorValue) * playRate;
            float clipStart = Mathf.Clamp(dragAnchorClipStart + deltaClip, 0f, dragAnchorClipEnd - MinSegmentDuration * playRate);
            float duration = Mathf.Max(MinSegmentDuration, (dragAnchorClipEnd - clipStart) / playRate);

            Undo.RecordObject(montage, "Trim Montage Segment Start");
            SerializedObject so = new(montage);
            SerializedProperty segmentProperty = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("startTime").floatValue = dragAnchorSegmentEnd - duration;
            segmentProperty.FindPropertyRelative("clipStartTime").floatValue = clipStart;
            segmentProperty.FindPropertyRelative("clipEndTime").floatValue = dragAnchorClipEnd;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplySegmentTrimEnd(int segmentIndex, float endTime)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || segmentIndex < 0 || segmentIndex >= montage.Segments.Count)
                return;

            MontageSegment segment = montage.Segments[segmentIndex];
            if (segment?.Clip == null)
                return;

            float playRate = segment.PlayRate;
            float minEndTime = segment.StartTime + MinSegmentDuration;
            endTime = Snap(Mathf.Clamp(endTime, minEndTime, segment.StartTime + (segment.Clip.length - dragAnchorClipStart) / playRate));
            float clipEnd = Mathf.Clamp(dragAnchorClipStart + (endTime - segment.StartTime) * playRate, dragAnchorClipStart + MinSegmentDuration * playRate, segment.Clip.length);

            Undo.RecordObject(montage, "Trim Montage Segment End");
            SerializedObject so = new(montage);
            SerializedProperty segmentProperty = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("clipStartTime").floatValue = dragAnchorClipStart;
            segmentProperty.FindPropertyRelative("clipEndTime").floatValue = clipEnd;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplySegmentTrack(int segmentIndex, Vector2 local)
        {
            if (context.Montage == null
                || segmentIndex < 0
                || segmentIndex >= context.Montage.Segments.Count
                || !TryGetTrackRow(local, TrackKind.Segment, out TrackRowLayout row))
                return;

            MontageSegment segment = context.Montage.Segments[segmentIndex];
            if (segment == null || segment.TrackId == row.TrackId)
                return;

            Undo.RecordObject(context.Montage, "Move Segment To Track");
            SerializedObject so = new(context.Montage);
            SerializedProperty segmentProperty = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("trackId").stringValue = row.TrackId;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private float SnapSegmentStartTime(int segmentIndex, float startTime)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || segmentIndex < 0 || segmentIndex >= montage.Segments.Count)
                return Snap(startTime);

            MontageSegment moving = montage.Segments[segmentIndex];
            if (moving == null)
                return Snap(startTime);

            float duration = moving.Duration;
            float endTime = startTime + duration;
            float tolerance = MagneticSnapPixels / Mathf.Max(1f, pixelsPerSecond);
            float bestDistance = tolerance;
            float snappedStart = startTime;
            bool snapped = false;

            if (startTime <= tolerance)
            {
                bestDistance = startTime;
                snappedStart = 0f;
                snapped = true;
            }

            for (int i = 0; i < montage.Segments.Count; i++)
            {
                if (i == segmentIndex)
                    continue;

                MontageSegment other = montage.Segments[i];
                if (other == null || other.TrackId != moving.TrackId)
                    continue;

                TrySnapEdge(other.EndTime, startTime, other.EndTime);
                TrySnapEdge(other.StartTime, endTime, other.StartTime - duration);
            }

            if (snapped)
            {
                snapGuideTime = Mathf.Max(0f, Mathf.Abs(snappedStart - startTime) <= Mathf.Abs(snappedStart + duration - endTime)
                    ? snappedStart
                    : snappedStart + duration);
                hasSnapGuide = true;
                return Mathf.Max(0f, snappedStart);
            }

            return Snap(startTime);

            void TrySnapEdge(float guideTime, float movingEdgeTime, float candidateStart)
            {
                float distance = Mathf.Abs(movingEdgeTime - guideTime);
                if (distance > bestDistance)
                    return;

                bestDistance = distance;
                snappedStart = Mathf.Max(0f, candidateStart);
                snapGuideTime = guideTime;
                snapped = true;
            }
        }

        private void ApplyNotifyTime(int notifyIndex, float time)
        {
            Undo.RecordObject(context.Montage, "Move Anim Notify");
            context.Montage.Notifies[notifyIndex].Time = Snap(time);
            context.MarkDirty();
        }

        private void ApplyNotifyTrack(int notifyIndex, Vector2 local)
        {
            if (context.Montage == null
                || notifyIndex < 0
                || notifyIndex >= context.Montage.Notifies.Count
                || !TryGetTrackRow(local, TrackKind.Notify, out TrackRowLayout row))
                return;

            AnimNotifyPlacement placement = context.Montage.Notifies[notifyIndex];
            if (placement == null || placement.TrackId == row.TrackId)
                return;

            Undo.RecordObject(context.Montage, "Move Notify To Track");
            SerializedObject so = new(context.Montage);
            SerializedProperty notifyProperty = so.FindProperty("notifies").GetArrayElementAtIndex(notifyIndex);
            notifyProperty.FindPropertyRelative("trackId").stringValue = row.TrackId;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplyNotifyStateRange(int notifyStateIndex, float startTime, float endTime)
        {
            startTime = Snap(Mathf.Max(0f, startTime));
            endTime = Snap(Mathf.Max(startTime + MinSegmentDuration, endTime));
            float maxTime = context.Montage.Length;
            endTime = Mathf.Min(endTime, maxTime);

            Undo.RecordObject(context.Montage, "Adjust Notify State");
            AnimNotifyStatePlacement placement = context.Montage.NotifyStates[notifyStateIndex];
            placement.StartTime = startTime;
            placement.EndTime = endTime;
            context.MarkDirty();
        }

        private void ApplyNotifyStateTrack(int notifyStateIndex, Vector2 local)
        {
            if (context.Montage == null
                || notifyStateIndex < 0
                || notifyStateIndex >= context.Montage.NotifyStates.Count
                || !TryGetTrackRow(local, TrackKind.NotifyState, out TrackRowLayout row))
                return;

            AnimNotifyStatePlacement placement = context.Montage.NotifyStates[notifyStateIndex];
            if (placement == null || placement.TrackId == row.TrackId)
                return;

            Undo.RecordObject(context.Montage, "Move Notify State To Track");
            SerializedObject so = new(context.Montage);
            SerializedProperty stateProperty = so.FindProperty("notifyStates").GetArrayElementAtIndex(notifyStateIndex);
            stateProperty.FindPropertyRelative("trackId").stringValue = row.TrackId;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void AddNotifyAtTime(float time) => AddNotifyAtTime(time, null, "Default");

        private void AddNotifyAtTime(float time, AnimNotifySO notify) => AddNotifyAtTime(time, notify, "Default");

        private void AddNotifyAtTime(float time, AnimNotifySO notify, string trackId)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Add Anim Notify");
            var so = new SerializedObject(montage);
            SerializedProperty prop = so.FindProperty("notifies");
            int index = prop.arraySize;
            prop.InsertArrayElementAtIndex(index);
            SerializedProperty element = prop.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("time").floatValue = Snap(time);
            element.FindPropertyRelative("notify").objectReferenceValue = notify;
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            so.ApplyModifiedPropertiesWithoutUndo();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedNotify(index);
        }

        private void AddNotifyStateAtTime(float time) => AddNotifyStateAtTime(time, null, "Default");

        private void AddNotifyStateAtTime(float time, AnimNotifyStateSO notifyState) => AddNotifyStateAtTime(time, notifyState, "Default");

        private void AddNotifyStateAtTime(float time, AnimNotifyStateSO notifyState, string trackId)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Add Anim Notify State");
            SerializedObject so = new(montage);
            SerializedProperty prop = so.FindProperty("notifyStates");
            int index = prop.arraySize;
            prop.InsertArrayElementAtIndex(index);
            SerializedProperty element = prop.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("startTime").floatValue = Snap(time);
            element.FindPropertyRelative("endTime").floatValue = Snap(time + DefaultQuickBlendDuration);
            element.FindPropertyRelative("notifyState").objectReferenceValue = notifyState;
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedNotifyState(index);
        }

        private void AddSegmentAtTime(float time, AnimationClip clip) => AddSegmentAtTime(time, clip, "Default");

        private void AddSegmentAtTime(float time, AnimationClip clip, string trackId)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Add Montage Segment");
            SerializedObject so = new(montage);
            SerializedProperty prop = so.FindProperty("segments");
            int index = prop.arraySize;
            prop.InsertArrayElementAtIndex(index);
            SerializedProperty element = prop.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("sectionName").stringValue = clip != null ? clip.name : "Default";
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            element.FindPropertyRelative("clip").objectReferenceValue = clip;
            element.FindPropertyRelative("startTime").floatValue = Snap(time);
            element.FindPropertyRelative("clipStartTime").floatValue = 0f;
            element.FindPropertyRelative("clipEndTime").floatValue = clip != null ? clip.length : 0f;
            element.FindPropertyRelative("playRate").floatValue = 1f;
            element.FindPropertyRelative("blendIn").floatValue = 0f;
            element.FindPropertyRelative("blendOut").floatValue = 0f;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedSegment(index);
        }

        private void ReplaceSegmentClip(int segmentIndex, AnimationClip clip)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || segmentIndex < 0 || segmentIndex >= montage.Segments.Count)
                return;

            Undo.RecordObject(montage, "Replace Montage Segment Clip");
            SerializedObject so = new(montage);
            SerializedProperty segment = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segment.FindPropertyRelative("sectionName").stringValue = clip != null ? clip.name : "Default";
            segment.FindPropertyRelative("clip").objectReferenceValue = clip;
            segment.FindPropertyRelative("clipStartTime").floatValue = 0f;
            segment.FindPropertyRelative("clipEndTime").floatValue = clip != null ? clip.length : 0f;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedSegment(segmentIndex);
        }

        private void ResetSegmentTrim(int segmentIndex)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || segmentIndex < 0 || segmentIndex >= montage.Segments.Count)
                return;

            MontageSegment montageSegment = montage.Segments[segmentIndex];
            AnimationClip clip = montageSegment?.Clip;
            if (clip == null)
                return;

            Undo.RecordObject(montage, "Reset Montage Segment Trim");
            SerializedObject so = new(montage);
            SerializedProperty segment = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segment.FindPropertyRelative("clipStartTime").floatValue = 0f;
            segment.FindPropertyRelative("clipEndTime").floatValue = clip.length;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedSegment(segmentIndex);
        }

        private void ReplaceNotify(int notifyIndex, AnimNotifySO notify)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || notifyIndex < 0 || notifyIndex >= montage.Notifies.Count)
                return;

            Undo.RecordObject(montage, "Replace Anim Notify");
            SerializedObject so = new(montage);
            SerializedProperty placement = so.FindProperty("notifies").GetArrayElementAtIndex(notifyIndex);
            placement.FindPropertyRelative("notify").objectReferenceValue = notify;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedNotify(notifyIndex);
        }

        private void ReplaceNotifyState(int notifyStateIndex, AnimNotifyStateSO notifyState)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || notifyStateIndex < 0 || notifyStateIndex >= montage.NotifyStates.Count)
                return;

            Undo.RecordObject(montage, "Replace Anim Notify State");
            SerializedObject so = new(montage);
            SerializedProperty placement = so.FindProperty("notifyStates").GetArrayElementAtIndex(notifyStateIndex);
            placement.FindPropertyRelative("notifyState").objectReferenceValue = notifyState;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedNotifyState(notifyStateIndex);
        }

        private bool DeleteSelected()
        {
            if (context.Montage == null)
                return false;

            if (context.SelectedSegmentIndices.Count > 0
                || context.SelectedNotifyIndices.Count > 0
                || context.SelectedNotifyStateIndices.Count > 0)
                return DeleteSelectedTimelineElements();

            return false;
        }

        private bool DeleteHoveredTrack()
        {
            if (context.Montage == null || !hasHoverTrack || hoverTrackId == "Default")
                return false;

            string propertyName = GetTrackPropertyName(hoverTrackKind);
            DeleteTrack(hoverTrackKind, propertyName, hoverTrackId);
            return true;
        }

        private bool DeleteArrayElements(string propertyName, IReadOnlyCollection<int> indices, string undoName)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || indices.Count == 0)
                return false;

            Undo.RecordObject(montage, undoName);
            SerializedObject so = new(montage);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
                return false;

            var sorted = new List<int>(indices);
            sorted.Sort((a, b) => b.CompareTo(a));
            for (int i = 0; i < sorted.Count; i++)
            {
                int index = sorted[i];
                if (index >= 0 && index < prop.arraySize)
                    prop.DeleteArrayElementAtIndex(index);
            }

            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelected(montage);
            return true;
        }

        private bool DeleteSelectedTimelineElements()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return false;

            Undo.RecordObject(montage, "Delete Montage Timeline Elements");
            SerializedObject so = new(montage);
            DeleteArrayElementsWithoutApply(so.FindProperty("segments"), context.SelectedSegmentIndices);
            DeleteArrayElementsWithoutApply(so.FindProperty("notifies"), context.SelectedNotifyIndices);
            DeleteArrayElementsWithoutApply(so.FindProperty("notifyStates"), context.SelectedNotifyStateIndices);
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelected(montage);
            return true;
        }

        private static void DeleteArrayElementsWithoutApply(SerializedProperty prop, IReadOnlyCollection<int> indices)
        {
            if (prop == null || indices.Count == 0)
                return;

            var sorted = new List<int>(indices);
            sorted.Sort((a, b) => b.CompareTo(a));
            for (int i = 0; i < sorted.Count; i++)
            {
                int index = sorted[i];
                if (index >= 0 && index < prop.arraySize)
                    prop.DeleteArrayElementAtIndex(index);
            }
        }

        private bool DeleteArrayElement(string propertyName, int index, string undoName)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || index < 0)
                return false;

            Undo.RecordObject(montage, undoName);
            SerializedObject so = new(montage);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || index >= prop.arraySize)
                return false;

            prop.DeleteArrayElementAtIndex(index);
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelected(montage);
            return true;
        }

        private void AddTrack(string propertyName, string displayName)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Add Montage Track");
            SerializedObject so = new(montage);
            SerializedProperty tracks = so.FindProperty(propertyName);
            if (tracks == null)
                return;

            int index = tracks.arraySize;
            tracks.InsertArrayElementAtIndex(index);
            string trackId = CreateUniqueTrackId(tracks, displayName);
            tracks.GetArrayElementAtIndex(index).stringValue = trackId;
            SerializedProperty order = so.FindProperty("timelineTrackOrder");
            if (order != null && TryGetTrackKindByPropertyName(propertyName, out TrackKind kind))
                EnsureTrackKeyInOrder(order, GetTrackKey(kind, trackId));

            so.ApplyModifiedProperties();
            context.MarkDirty();
            MarkDirtyRepaint();
        }

        private void DeleteTrack(TrackKind kind, string propertyName, string trackId)
        {
            AnimMontageSO montage = context.Montage;
            trackId = SanitizeTrackId(trackId);
            if (montage == null || trackId == "Default")
                return;

            if (!EditorUtility.DisplayDialog(
                    "Delete Montage Track",
                    $"Delete '{trackId}' track and all items on it?",
                    "Delete",
                    "Cancel"))
                return;

            Undo.RecordObject(montage, "Delete Montage Track");
            SerializedObject so = new(montage);
            DeleteTrackItems(so, kind, trackId);

            SerializedProperty tracks = so.FindProperty(propertyName);
            if (tracks != null)
            {
                for (int i = tracks.arraySize - 1; i >= 0; i--)
                {
                    if (SanitizeTrackId(tracks.GetArrayElementAtIndex(i).stringValue) == trackId)
                        tracks.DeleteArrayElementAtIndex(i);
                }
            }

            SerializedProperty order = so.FindProperty("timelineTrackOrder");
            if (order != null)
                RemoveTrackKeyFromOrder(order, GetTrackKey(kind, trackId));

            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelected(montage);
            MarkDirtyRepaint();
        }

        private void ApplyTrackReorder(Vector2 local)
        {
            UpdateHoverTrack(local);
            if (context.Montage == null
                || string.IsNullOrEmpty(dragTrackId)
                || !TryGetTrackRow(local, out TrackRowLayout targetRow)
                || (targetRow.Kind == dragTrackKind && targetRow.TrackId == dragTrackId))
                return;

            MoveTrack(dragTrackKind, dragTrackId, targetRow.Kind, targetRow.TrackId);
        }

        private void MoveTrack(TrackKind sourceKind, string sourceTrackId, TrackKind targetKind, string targetTrackId)
        {
            sourceTrackId = SanitizeTrackId(sourceTrackId);
            targetTrackId = SanitizeTrackId(targetTrackId);
            if (sourceKind == targetKind && sourceTrackId == targetTrackId)
                return;

            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Reorder Montage Track");
            SerializedObject so = new(montage);
            EnsureTrackInProperty(so.FindProperty(GetTrackPropertyName(sourceKind)), sourceTrackId);
            EnsureTrackInProperty(so.FindProperty(GetTrackPropertyName(targetKind)), targetTrackId);

            SerializedProperty order = so.FindProperty("timelineTrackOrder");
            if (order == null)
                return;

            EnsureTimelineOrder(order, GetOrderedTimelineTracks(montage));
            string sourceKey = GetTrackKey(sourceKind, sourceTrackId);
            string targetKey = GetTrackKey(targetKind, targetTrackId);
            EnsureTrackKeyInOrder(order, sourceKey);
            EnsureTrackKeyInOrder(order, targetKey);

            int sourceIndex = FindTrackKeyIndex(order, sourceKey);
            int targetIndex = FindTrackKeyIndex(order, targetKey);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
                return;

            order.MoveArrayElement(sourceIndex, targetIndex);
            so.ApplyModifiedProperties();
            context.MarkDirty();
            MarkDirtyRepaint();
        }

        private void ApplyBoxSelection()
        {
            Rect selectionRect = GetBoxSelectionRect();
            if (selectionRect.width < 3f && selectionRect.height < 3f)
                return;

            var segmentSelection = new List<int>();
            for (int i = 0; i < segmentLayouts.Count; i++)
            {
                SegmentLayout layout = segmentLayouts[i];
                if (layout.Body.Overlaps(selectionRect))
                    segmentSelection.Add(layout.Index);
            }

            var notifySelection = new List<int>();
            for (int i = 0; i < notifyLayouts.Count; i++)
            {
                NotifyLayout layout = notifyLayouts[i];
                if (layout.Body.Overlaps(selectionRect))
                    notifySelection.Add(layout.Index);
            }

            var stateSelection = new List<int>();
            for (int i = 0; i < notifyStateLayouts.Count; i++)
            {
                NotifyStateLayout layout = notifyStateLayouts[i];
                if (layout.Body.Overlaps(selectionRect))
                    stateSelection.Add(layout.Index);
            }

            var trackSelection = new List<string>();
            for (int i = 0; i < trackRows.Count; i++)
            {
                TrackRowLayout row = trackRows[i];
                var labelRect = new Rect(row.Rect.xMin, row.Rect.yMin, TrackLabelWidth, row.Rect.height);
                if (labelRect.Overlaps(selectionRect))
                    trackSelection.Add(GetTrackKey(row.Kind, row.TrackId));
            }

            if (segmentSelection.Count > 0 || notifySelection.Count > 0 || stateSelection.Count > 0 || trackSelection.Count > 0)
                context.SetSelectedTimelineElements(segmentSelection, notifySelection, stateSelection, trackSelection, boxSelectAdditive);
            else if (!boxSelectAdditive)
                context.SetSelected(context.Montage);
        }

        private Rect GetBoxSelectionRect()
        {
            float xMin = Mathf.Min(boxSelectStart.x, boxSelectEnd.x);
            float yMin = Mathf.Min(boxSelectStart.y, boxSelectEnd.y);
            float xMax = Mathf.Max(boxSelectStart.x, boxSelectEnd.x);
            float yMax = Mathf.Max(boxSelectStart.y, boxSelectEnd.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void DeleteTrackItems(SerializedObject so, TrackKind kind, string trackId)
        {
            string itemPropertyName = kind switch
            {
                TrackKind.Segment => "segments",
                TrackKind.Notify => "notifies",
                TrackKind.NotifyState => "notifyStates",
                _ => null
            };

            if (string.IsNullOrEmpty(itemPropertyName))
                return;

            SerializedProperty items = so.FindProperty(itemPropertyName);
            if (items == null)
                return;

            for (int i = items.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                SerializedProperty itemTrack = item.FindPropertyRelative("trackId");
                if (itemTrack != null && SanitizeTrackId(itemTrack.stringValue) == trackId)
                    items.DeleteArrayElementAtIndex(i);
            }
        }

        private static void EnsureTrackInProperty(SerializedProperty tracks, string trackId)
        {
            if (tracks == null)
                return;

            if (FindTrackIndex(tracks, trackId) >= 0)
                return;

            int index = tracks.arraySize;
            tracks.InsertArrayElementAtIndex(index);
            tracks.GetArrayElementAtIndex(index).stringValue = SanitizeTrackId(trackId);
        }

        private static int FindTrackIndex(SerializedProperty tracks, string trackId)
        {
            trackId = SanitizeTrackId(trackId);
            for (int i = 0; i < tracks.arraySize; i++)
            {
                if (SanitizeTrackId(tracks.GetArrayElementAtIndex(i).stringValue) == trackId)
                    return i;
            }

            return -1;
        }

        private static void EnsureTimelineOrder(SerializedProperty order, List<TrackIdentity> tracks)
        {
            for (int i = 0; i < tracks.Count; i++)
                EnsureTrackKeyInOrder(order, GetTrackKey(tracks[i].Kind, tracks[i].TrackId));
        }

        private static void EnsureTrackKeyInOrder(SerializedProperty order, string key)
        {
            if (FindTrackKeyIndex(order, key) >= 0)
                return;

            int index = order.arraySize;
            order.InsertArrayElementAtIndex(index);
            order.GetArrayElementAtIndex(index).stringValue = key;
        }

        private static int FindTrackKeyIndex(SerializedProperty order, string key)
        {
            for (int i = 0; i < order.arraySize; i++)
            {
                if (order.GetArrayElementAtIndex(i).stringValue == key)
                    return i;
            }

            return -1;
        }

        private static void RemoveTrackKeyFromOrder(SerializedProperty order, string key)
        {
            for (int i = order.arraySize - 1; i >= 0; i--)
            {
                if (order.GetArrayElementAtIndex(i).stringValue == key)
                    order.DeleteArrayElementAtIndex(i);
            }
        }

        private static string CreateUniqueTrackId(SerializedProperty tracks, string displayName)
        {
            int suffix = Mathf.Max(1, tracks.arraySize);
            while (true)
            {
                string candidate = $"{displayName} {suffix}";
                bool exists = false;
                for (int i = 0; i < tracks.arraySize; i++)
                {
                    if (SanitizeTrackId(tracks.GetArrayElementAtIndex(i).stringValue) == candidate)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    return candidate;

                suffix++;
            }
        }

        private static string GetTrackPropertyName(TrackKind kind) =>
            kind switch
            {
                TrackKind.Segment => "animationTracks",
                TrackKind.Notify => "notifyTracks",
                TrackKind.NotifyState => "notifyStateTracks",
                _ => string.Empty
            };

        private static bool TryGetTrackKindByPropertyName(string propertyName, out TrackKind kind)
        {
            switch (propertyName)
            {
                case "animationTracks":
                    kind = TrackKind.Segment;
                    return true;

                case "notifyTracks":
                    kind = TrackKind.Notify;
                    return true;

                case "notifyStateTracks":
                    kind = TrackKind.NotifyState;
                    return true;

                default:
                    kind = default;
                    return false;
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            segmentLayouts.Clear();
            notifyLayouts.Clear();
            notifyStateLayouts.Clear();
            trackRows.Clear();

            var painter = ctx.painter2D;
            DrawBackground(painter, rect);

            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            DrawRuler(painter, rect, montage.Length);
            DrawTimeGrid(painter, rect, montage.Length);
            List<TrackIdentity> orderedTracks = GetOrderedTimelineTracks(montage);
            totalTrackContentHeight = orderedTracks.Count * (TrackHeight + TrackGap);
            ClampViewStartY();

            float y = RulerHeight + TrackGap - viewStartY;
            foreach (TrackIdentity track in orderedTracks)
            {
                switch (track.Kind)
                {
                    case TrackKind.Segment:
                        y = DrawSegmentTrack(painter, rect, y, montage, track.TrackId);
                        break;

                    case TrackKind.Notify:
                        y = DrawNotifyTrack(painter, rect, y, montage, track.TrackId);
                        break;

                    case TrackKind.NotifyState:
                        y = DrawNotifyStateTrack(painter, rect, y, montage, track.TrackId);
                        break;
                }
            }

            DrawSnapGuide(painter, rect);
            DrawPlayhead(painter, rect);
            DrawBoxSelection(painter);
        }

        private void DrawBackground(Painter2D painter, Rect rect)
        {
            painter.fillColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawRuler(Painter2D painter, Rect rect, float length)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.15f);
            painter.lineWidth = 1f;
            float y = rect.yMin + RulerHeight;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + TrackLabelWidth, y));
            painter.LineTo(new Vector2(rect.xMax, y));
            painter.Stroke();

            float maxTime = Mathf.Max(length, 1f);
            int step = pixelsPerSecond >= 100f ? 1 : 5;
            for (float t = 0f; t <= maxTime; t += step)
            {
                float x = TimeToX(t);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.yMin + 4f));
                painter.LineTo(new Vector2(x, y));
                painter.Stroke();
            }

            var labelGutter = new Rect(rect.xMin, rect.yMin, TrackLabelWidth, RulerHeight);
            painter.fillColor = new Color(0.09f, 0.09f, 0.1f, 1f);
            FillRect(painter, labelGutter);
        }

        private void DrawTimeGrid(Painter2D painter, Rect rect, float length)
        {
            float maxTime = Mathf.Max(length, 1f);
            float minorStep = pixelsPerSecond >= 180f ? 0.25f : pixelsPerSecond >= 90f ? 0.5f : 1f;
            float majorStep = pixelsPerSecond >= 90f ? 1f : 5f;

            for (float t = 0f; t <= maxTime; t += minorStep)
            {
                bool major = Mathf.Abs(t / majorStep - Mathf.Round(t / majorStep)) < 0.001f;
                painter.strokeColor = major
                    ? new Color(1f, 1f, 1f, 0.09f)
                    : new Color(1f, 1f, 1f, 0.035f);
                painter.lineWidth = major ? 1.2f : 1f;
                float x = TimeToX(t);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, RulerHeight));
                painter.LineTo(new Vector2(x, rect.yMax));
                painter.Stroke();
            }
        }

        private float DrawSegmentTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.34f, 0.62f, 0.35f), TrackKind.Segment, trackId);
            trackRows.Add(new TrackRowLayout(TrackKind.Segment, trackId, new Rect(rect.xMin, y, rect.width, TrackHeight)));
            Rect contentRect = GetTimelineContentRect(rect);

            List<int> segmentIndices = GetSegmentIndicesForTrack(montage, trackId);
            for (int i = 0; i < segmentIndices.Count; i++)
            {
                int segmentIndex = segmentIndices[i];
                MontageSegment segment = montage.Segments[segmentIndex];
                if (segment?.Clip == null)
                    continue;

                float x0 = TimeToX(segment.StartTime);
                float x1 = TimeToX(segment.EndTime);
                var body = new Rect(x0, y + 2f, Mathf.Max(4f, x1 - x0), TrackHeight - 4f);
                if (!TryClipRect(body, contentRect, out Rect clippedBody))
                    continue;

                bool selected = context.IsSegmentSelected(segmentIndex);

                painter.fillColor = selected ? SegmentSelectedColor : SegmentCoreColor;
                FillRoundedRect(painter, clippedBody, 3f);
                painter.strokeColor = selected ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.22f);
                painter.lineWidth = selected ? 1.6f : 1f;
                StrokeRoundedRect(painter, clippedBody, 3f);

                DrawSegmentAutoBlendOverlay(painter, clippedBody, montage, segment, segmentIndex, contentRect);

                if (clippedBody.width > 28f)
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
                    painter.lineWidth = 1f;
                    float midY = clippedBody.yMin + clippedBody.height * 0.5f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(clippedBody.xMin + 6f, midY));
                    painter.LineTo(new Vector2(clippedBody.xMax - 6f, midY));
                    painter.Stroke();
                }

                DrawSegmentEdgeTicks(painter, clippedBody);

                segmentLayouts.Add(new SegmentLayout(segmentIndex, clippedBody));
            }

            return y + TrackHeight + TrackGap;
        }

        private float DrawNotifyTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.62f, 0.72f, 0.22f), TrackKind.Notify, trackId);
            trackRows.Add(new TrackRowLayout(TrackKind.Notify, trackId, new Rect(rect.xMin, y, rect.width, TrackHeight)));
            Rect contentRect = GetTimelineContentRect(rect);
            for (int i = 0; i < montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement placement = montage.Notifies[i];
                if (placement == null || placement.TrackId != trackId)
                    continue;

                Color color = placement.Notify != null ? placement.Notify.EditorColor : new Color(0.4f, 0.8f, 1f);
                float x = TimeToX(placement.Time);
                if (x < contentRect.xMin - 8f || x > contentRect.xMax + 8f)
                    continue;

                DrawDiamond(painter, x, y + TrackHeight * 0.5f, 7f, color);
                var hitRect = new Rect(x - 9f, y + TrackHeight * 0.5f - 9f, 18f, 18f);
                notifyLayouts.Add(new NotifyLayout(i, hitRect));
                if (context.IsNotifySelected(i))
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.78f);
                    painter.lineWidth = 1.5f;
                    StrokeDiamond(painter, x, y + TrackHeight * 0.5f, 9.5f);
                }
            }

            return y + TrackHeight + TrackGap;
        }

        private float DrawNotifyStateTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.28f, 0.72f, 0.42f, 0.18f), TrackKind.NotifyState, trackId);
            trackRows.Add(new TrackRowLayout(TrackKind.NotifyState, trackId, new Rect(rect.xMin, y, rect.width, TrackHeight)));
            Rect contentRect = GetTimelineContentRect(rect);
            for (int i = 0; i < montage.NotifyStates.Count; i++)
            {
                AnimNotifyStatePlacement placement = montage.NotifyStates[i];
                if (placement == null || placement.TrackId != trackId || placement.NotifyState == null)
                    continue;

                float x0 = TimeToX(placement.StartTime);
                float x1 = TimeToX(placement.EndTime);
                var bar = new Rect(x0, y, Mathf.Max(4f, x1 - x0), TrackHeight);
                if (!TryClipRect(bar, contentRect, out Rect clippedBar))
                    continue;

                bool selected = context.IsNotifyStateSelected(i);
                painter.fillColor = selected
                    ? placement.NotifyState.EditorColor * new Color(1f, 1f, 1f, 0.85f)
                    : placement.NotifyState.EditorColor * new Color(1f, 1f, 1f, 0.55f);
                FillRoundedRect(painter, clippedBar, 3f);
                notifyStateLayouts.Add(new NotifyStateLayout(i, clippedBar));
            }

            return y + TrackHeight + TrackGap;
        }

        private void DrawTrackRow(Painter2D painter, Rect rect, float y, Color accentColor, TrackKind kind, string trackId)
        {
            bool hovered = hasHoverTrack && hoverTrackKind == kind && hoverTrackId == trackId;
            bool selected = context.IsTimelineTrackSelected(GetTrackKey(kind, trackId));
            var row = new Rect(rect.xMin + TrackLabelWidth, y, rect.width - TrackLabelWidth, TrackHeight);
            painter.fillColor = hovered ? new Color(0.16f, 0.16f, 0.18f, 1f) : TrackRowColor;
            FillRect(painter, row);

            var labelRect = new Rect(rect.xMin + 2f, y + 1f, TrackLabelWidth - 4f, TrackHeight - 2f);
            painter.fillColor = hovered || selected
                ? accentColor * new Color(1.25f, 1.25f, 1.25f, 1.55f)
                : accentColor;
            FillRoundedRect(painter, labelRect, 3f);
            if (hovered || selected)
            {
                painter.strokeColor = selected ? new Color(1f, 0.86f, 0.28f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
                painter.lineWidth = selected ? 1.8f : 1.4f;
                StrokeRoundedRect(painter, labelRect, 3f);
            }

            DrawTrackGrip(painter, labelRect);
        }

        private static void DrawTrackGrip(Painter2D painter, Rect rect)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.45f);
            painter.lineWidth = 1.5f;
            float centerY = rect.center.y;
            for (int i = -1; i <= 1; i++)
            {
                float y = centerY + i * 6f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin + 14f, y));
                painter.LineTo(new Vector2(rect.xMax - 14f, y));
                painter.Stroke();
            }
        }

        private void DrawSegmentAutoBlendOverlay(
            Painter2D painter,
            Rect body,
            AnimMontageSO montage,
            MontageSegment segment,
            int segmentIndex,
            Rect contentRect)
        {
            for (int i = 0; i < montage.Segments.Count; i++)
            {
                if (i == segmentIndex)
                    continue;

                MontageSegment other = montage.Segments[i];
                if (other?.Clip == null)
                    continue;

                float overlapStart = Mathf.Max(segment.StartTime, other.StartTime);
                float overlapEnd = Mathf.Min(segment.EndTime, other.EndTime);
                if (overlapEnd <= overlapStart)
                    continue;

                float x0 = Mathf.Max(body.xMin, contentRect.xMin, TimeToX(overlapStart));
                float x1 = Mathf.Min(body.xMax, contentRect.xMax, TimeToX(overlapEnd));
                if (x1 <= x0)
                    continue;

                var blendRect = new Rect(x0, body.yMin + 6f, x1 - x0, body.height - 12f);
                painter.fillColor = AutoBlendOverlayColor;
                FillRoundedRect(painter, blendRect, 2f);
                painter.strokeColor = new Color(1f, 0.88f, 0.36f, 0.82f);
                painter.lineWidth = 1.3f;
                StrokeRoundedRect(painter, blendRect, 2f);
                DrawDiagonalHatch(painter, blendRect, new Color(1f, 1f, 1f, 0.22f));
            }
        }

        private static void DrawSegmentEdgeTicks(Painter2D painter, Rect rect)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.32f);
            painter.lineWidth = 1f;

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? rect.xMin + 3f : rect.xMax - 3f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.yMin + 5f));
                painter.LineTo(new Vector2(x, rect.yMax - 5f));
                painter.Stroke();
            }
        }

        private static void DrawDiagonalHatch(Painter2D painter, Rect rect, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1f;
            const float step = 6f;
            for (float offset = rect.xMin - rect.height; offset < rect.xMax; offset += step)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(offset, rect.yMax));
                painter.LineTo(new Vector2(offset + rect.height, rect.yMin));
                painter.Stroke();
            }
        }

        private static void FillRoundedRect(Painter2D painter, Rect rect, float radius)
        {
            radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
            if (radius <= 0f)
            {
                FillRect(painter, rect);
                return;
            }

            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax - radius, rect.yMin));
            painter.Arc(new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f);
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - radius));
            painter.Arc(new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            painter.LineTo(new Vector2(rect.xMin + radius, rect.yMax));
            painter.Arc(new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
            painter.LineTo(new Vector2(rect.xMin, rect.yMin + radius));
            painter.Arc(new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
            painter.ClosePath();
            painter.Fill();
        }

        private static void StrokeRoundedRect(Painter2D painter, Rect rect, float radius)
        {
            radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
            if (radius <= 0f)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Stroke();
                return;
            }

            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax - radius, rect.yMin));
            painter.Arc(new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f);
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - radius));
            painter.Arc(new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            painter.LineTo(new Vector2(rect.xMin + radius, rect.yMax));
            painter.Arc(new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
            painter.LineTo(new Vector2(rect.xMin, rect.yMin + radius));
            painter.Arc(new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
            painter.ClosePath();
            painter.Stroke();
        }

        private void DrawSnapGuide(Painter2D painter, Rect rect)
        {
            if (!hasSnapGuide)
                return;

            float x = TimeToX(snapGuideTime);
            painter.strokeColor = new Color(1f, 0.86f, 0.24f, 0.95f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, RulerHeight));
            painter.LineTo(new Vector2(x, rect.yMax));
            painter.Stroke();
        }

        private void DrawPlayhead(Painter2D painter, Rect rect)
        {
            float x = TimeToX(context.PlayheadTime);
            painter.strokeColor = new Color(1f, 0.35f, 0.35f, 0.95f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, rect.yMin));
            painter.LineTo(new Vector2(x, rect.yMax));
            painter.Stroke();
        }

        private void DrawBoxSelection(Painter2D painter)
        {
            if (dragMode != DragMode.BoxSelect)
                return;

            Rect rect = GetBoxSelectionRect();
            if (rect.width < 3f && rect.height < 3f)
                return;

            painter.fillColor = new Color(0.35f, 0.62f, 1f, 0.16f);
            FillRect(painter, rect);
            painter.strokeColor = new Color(0.68f, 0.84f, 1f, 0.9f);
            painter.lineWidth = 1.2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Stroke();
        }

        private static void FillRect(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static void DrawDiamond(Painter2D painter, float cx, float cy, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy - radius));
            painter.LineTo(new Vector2(cx + radius, cy));
            painter.LineTo(new Vector2(cx, cy + radius));
            painter.LineTo(new Vector2(cx - radius, cy));
            painter.ClosePath();
            painter.Fill();
        }

        private static void StrokeDiamond(Painter2D painter, float cx, float cy, float radius)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy - radius));
            painter.LineTo(new Vector2(cx + radius, cy));
            painter.LineTo(new Vector2(cx, cy + radius));
            painter.LineTo(new Vector2(cx - radius, cy));
            painter.ClosePath();
            painter.Stroke();
        }

        private bool TryHitPlayhead(Vector2 local, out float distance)
        {
            distance = Mathf.Abs(local.x - TimeToX(context.PlayheadTime));
            return distance <= 6f;
        }

        private bool TryHitSegment(Vector2 local, out int index, out DragMode mode)
        {
            index = -1;
            mode = DragMode.None;

            for (int i = segmentLayouts.Count - 1; i >= 0; i--)
            {
                SegmentLayout layout = segmentLayouts[i];
                if (!layout.Body.Contains(local))
                    continue;

                index = layout.Index;
                if (Mathf.Abs(local.x - layout.Body.xMin) <= EdgeHandleWidth)
                {
                    mode = DragMode.SegmentTrimStart;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMax) <= EdgeHandleWidth)
                {
                    mode = DragMode.SegmentTrimEnd;
                    return true;
                }

                mode = DragMode.SegmentMove;
                return true;
            }

            return false;
        }

        private bool TryHitNotify(Vector2 local, out int index)
        {
            index = -1;
            if (context.Montage == null || !TryGetTrackRow(local, TrackKind.Notify, out TrackRowLayout row))
                return false;

            float best = 999f;
            for (int i = 0; i < context.Montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement placement = context.Montage.Notifies[i];
                if (placement == null || placement.TrackId != row.TrackId)
                    continue;

                float dx = Mathf.Abs(local.x - TimeToX(placement.Time));
                if (dx < 8f && dx < best)
                {
                    best = dx;
                    index = i;
                }
            }

            return index >= 0;
        }

        private bool TryHitNotifyState(Vector2 local, out int index, out DragMode mode)
        {
            index = -1;
            mode = DragMode.None;

            for (int i = notifyStateLayouts.Count - 1; i >= 0; i--)
            {
                NotifyStateLayout layout = notifyStateLayouts[i];
                if (!layout.Body.Contains(local))
                    continue;

                index = layout.Index;
                if (Mathf.Abs(local.x - layout.Body.xMin) <= EdgeHandleWidth)
                {
                    mode = DragMode.NotifyStateResizeStart;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMax) <= EdgeHandleWidth)
                {
                    mode = DragMode.NotifyStateResizeEnd;
                    return true;
                }

                mode = DragMode.NotifyStateMove;
                return true;
            }

            return false;
        }

        private bool TryGetTrackRow(Vector2 local, out TrackRowLayout row)
        {
            for (int i = trackRows.Count - 1; i >= 0; i--)
            {
                row = trackRows[i];
                if (row.Rect.Contains(local))
                    return true;
            }

            row = default;
            return false;
        }

        private bool TryGetTrackRow(Vector2 local, TrackKind kind, out TrackRowLayout row)
        {
            if (!TryGetTrackRow(local, out row))
                return false;

            return row.Kind == kind;
        }

        private void UpdateHoverTrack(Vector2 local)
        {
            if (TryGetTrackRow(local, out TrackRowLayout row))
            {
                if (IsTrackLabel(local, row))
                {
                    bool changed = !hasHoverTrack || hoverTrackKind != row.Kind || hoverTrackId != row.TrackId;
                    hasHoverTrack = true;
                    hoverTrackKind = row.Kind;
                    hoverTrackId = row.TrackId;
                    UpdateHoverTooltip(local, new HoverTooltipInfo(GetTrackTypeName(row.Kind)));
                    if (changed)
                        MarkDirtyRepaint();

                    return;
                }

                if (TryGetHoverElementTooltip(local, out HoverTooltipInfo tooltip))
                {
                    if (hasHoverTrack)
                    {
                        hasHoverTrack = false;
                        MarkDirtyRepaint();
                    }

                    UpdateHoverTooltip(local, tooltip);
                    return;
                }

                if (hasHoverTrack)
                {
                    hasHoverTrack = false;
                    MarkDirtyRepaint();
                }

                HideHoverTooltip();

                return;
            }

            if (!hasHoverTrack)
                return;

            hasHoverTrack = false;
            HideHoverTooltip();
            MarkDirtyRepaint();
        }

        private bool IsHoveredTrack(TrackKind kind, string trackId) =>
            hasHoverTrack && hoverTrackKind == kind && hoverTrackId == trackId;

        private static bool IsTrackLabel(Vector2 local, TrackRowLayout row) =>
            local.x >= row.Rect.xMin && local.x <= row.Rect.xMin + TrackLabelWidth;

        private bool TryGetHoverElementTooltip(Vector2 local, out HoverTooltipInfo tooltip)
        {
            tooltip = default;
            if (context.Montage == null)
                return false;

            for (int i = segmentLayouts.Count - 1; i >= 0; i--)
            {
                SegmentLayout layout = segmentLayouts[i];
                if (!layout.Body.Contains(local))
                    continue;

                MontageSegment segment = context.Montage.Segments[layout.Index];
                string sectionName = string.IsNullOrEmpty(segment.SectionName) ? "Animation Clip" : segment.SectionName;
                tooltip = new HoverTooltipInfo(sectionName, 120f);
                return true;
            }

            for (int i = notifyStateLayouts.Count - 1; i >= 0; i--)
            {
                NotifyStateLayout layout = notifyStateLayouts[i];
                if (!layout.Body.Contains(local))
                    continue;

                AnimNotifyStatePlacement placement = context.Montage.NotifyStates[layout.Index];
                string stateName = placement.NotifyState != null ? placement.NotifyState.name : "Notify State";
                tooltip = new HoverTooltipInfo(stateName, 120f);
                return true;
            }

            if (TryHitNotify(local, out int notifyIndex))
            {
                AnimNotifyPlacement placement = context.Montage.Notifies[notifyIndex];
                string notifyName = placement.Notify != null ? placement.Notify.name : "Notify";
                tooltip = new HoverTooltipInfo(notifyName, 96f);
                return true;
            }

            return false;
        }

        private void UpdateHoverTooltip(Vector2 local, HoverTooltipInfo tooltip)
        {
            hoverTooltip.text = tooltip.Text;
            hoverTooltip.style.display = DisplayStyle.Flex;
            float width = Mathf.Clamp(hoverTooltip.text.Length * 7.5f + 18f, tooltip.MinWidth, 240f);
            hoverTooltip.style.width = width;
            hoverTooltip.style.left = Mathf.Clamp(local.x + 14f, 4f, Mathf.Max(4f, contentRect.width - width - 4f));
            hoverTooltip.style.top = Mathf.Clamp(local.y + 16f, RulerHeight + 4f, Mathf.Max(RulerHeight + 4f, contentRect.height - 28f));
        }

        private void HideHoverTooltip()
        {
            if (hoverTooltip != null)
                hoverTooltip.style.display = DisplayStyle.None;
        }

        private static string GetTrackTypeName(TrackKind kind) =>
            kind switch
            {
                TrackKind.Segment => "Animation",
                TrackKind.Notify => "Notify",
                TrackKind.NotifyState => "Notify State",
                _ => "Track"
            };

        private static List<TrackIdentity> GetOrderedTimelineTracks(AnimMontageSO montage)
        {
            List<TrackIdentity> tracks = GetAllTimelineTracks(montage);
            List<TrackIdentity> ordered = new();
            IReadOnlyList<string> order = montage.TimelineTrackOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (!TryParseTrackKey(order[i], out TrackIdentity identity))
                    continue;

                if (!ContainsTrack(tracks, identity.Kind, identity.TrackId)
                    || ContainsTrack(ordered, identity.Kind, identity.TrackId))
                    continue;

                ordered.Add(identity);
            }

            for (int i = 0; i < tracks.Count; i++)
            {
                TrackIdentity identity = tracks[i];
                if (!ContainsTrack(ordered, identity.Kind, identity.TrackId))
                    ordered.Add(identity);
            }

            return ordered;
        }

        private static List<TrackIdentity> GetAllTimelineTracks(AnimMontageSO montage)
        {
            List<TrackIdentity> tracks = new();
            AddTrackIdentities(tracks, TrackKind.Segment, GetAnimationTrackIds(montage));
            AddTrackIdentities(tracks, TrackKind.Notify, GetNotifyTrackIds(montage));
            AddTrackIdentities(tracks, TrackKind.NotifyState, GetNotifyStateTrackIds(montage));
            return tracks;
        }

        private static void AddTrackIdentities(List<TrackIdentity> tracks, TrackKind kind, List<string> trackIds)
        {
            for (int i = 0; i < trackIds.Count; i++)
            {
                string trackId = SanitizeTrackId(trackIds[i]);
                if (!ContainsTrack(tracks, kind, trackId))
                    tracks.Add(new TrackIdentity(kind, trackId));
            }
        }

        private static bool ContainsTrack(List<TrackIdentity> tracks, TrackKind kind, string trackId)
        {
            trackId = SanitizeTrackId(trackId);
            for (int i = 0; i < tracks.Count; i++)
            {
                TrackIdentity track = tracks[i];
                if (track.Kind == kind && track.TrackId == trackId)
                    return true;
            }

            return false;
        }

        private static string GetTrackKey(TrackKind kind, string trackId) =>
            $"{kind}:{SanitizeTrackId(trackId)}";

        private static bool TryParseTrackKey(string key, out TrackIdentity identity)
        {
            identity = default;
            if (string.IsNullOrEmpty(key))
                return false;

            int split = key.IndexOf(':');
            if (split <= 0 || split >= key.Length - 1)
                return false;

            if (!Enum.TryParse(key.Substring(0, split), out TrackKind kind))
                return false;

            identity = new TrackIdentity(kind, SanitizeTrackId(key.Substring(split + 1)));
            return true;
        }

        private static List<string> GetAnimationTrackIds(AnimMontageSO montage)
        {
            List<string> tracks = CreateTrackList(montage.AnimationTracks);
            for (int i = 0; i < montage.Segments.Count; i++)
            {
                MontageSegment segment = montage.Segments[i];
                if (segment != null)
                    AddTrackId(tracks, segment.TrackId);
            }

            return tracks;
        }

        private static List<string> GetNotifyTrackIds(AnimMontageSO montage)
        {
            List<string> tracks = CreateTrackList(montage.NotifyTracks);
            for (int i = 0; i < montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement notify = montage.Notifies[i];
                if (notify != null)
                    AddTrackId(tracks, notify.TrackId);
            }

            return tracks;
        }

        private static List<string> GetNotifyStateTrackIds(AnimMontageSO montage)
        {
            List<string> tracks = CreateTrackList(montage.NotifyStateTracks);
            for (int i = 0; i < montage.NotifyStates.Count; i++)
            {
                AnimNotifyStatePlacement state = montage.NotifyStates[i];
                if (state != null)
                    AddTrackId(tracks, state.TrackId);
            }

            return tracks;
        }

        private static List<string> CreateTrackList(IReadOnlyList<string> source)
        {
            List<string> tracks = new();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    AddTrackId(tracks, source[i]);
            }

            if (tracks.Count == 0)
                tracks.Add("Default");

            return tracks;
        }

        private static void AddTrackId(List<string> tracks, string trackId)
        {
            trackId = SanitizeTrackId(trackId);
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] == trackId)
                    return;
            }

            tracks.Add(trackId);
        }

        private static List<int> GetSegmentIndicesForTrack(AnimMontageSO montage, string trackId)
        {
            List<int> indices = new();
            trackId = SanitizeTrackId(trackId);
            for (int i = 0; i < montage.Segments.Count; i++)
            {
                MontageSegment segment = montage.Segments[i];
                if (segment != null && segment.TrackId == trackId)
                    indices.Add(i);
            }

            indices.Sort((left, right) =>
            {
                MontageSegment leftSegment = montage.Segments[left];
                MontageSegment rightSegment = montage.Segments[right];
                int timeCompare = leftSegment.StartTime.CompareTo(rightSegment.StartTime);
                return timeCompare != 0 ? timeCompare : left.CompareTo(right);
            });

            return indices;
        }

        private static string SanitizeTrackId(string trackId) =>
            string.IsNullOrEmpty(trackId) ? "Default" : trackId;

        private static Rect GetTimelineContentRect(Rect rect) =>
            new(rect.xMin + TrackLabelWidth + ContentPadding, RulerHeight, rect.width - TrackLabelWidth - ContentPadding, rect.height - RulerHeight);

        private static bool TryClipRect(Rect rect, Rect clip, out Rect clipped)
        {
            float xMin = Mathf.Max(rect.xMin, clip.xMin);
            float yMin = Mathf.Max(rect.yMin, clip.yMin);
            float xMax = Mathf.Min(rect.xMax, clip.xMax);
            float yMax = Mathf.Min(rect.yMax, clip.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                clipped = default;
                return false;
            }

            clipped = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        private void SetPlayheadFromX(float x) => context.SetPlayhead(XToTime(x));

        private void ClampViewStartTime()
        {
            float visibleDuration = Mathf.Max(0f, (contentRect.width - TrackLabelWidth - ContentPadding) / Mathf.Max(1f, pixelsPerSecond));
            float maxStart = Mathf.Max(0f, (context.Montage?.Length ?? 0f) - visibleDuration * 0.35f);
            viewStartTime = Mathf.Clamp(viewStartTime, 0f, maxStart);
        }

        private void ClampViewStartY()
        {
            float visibleHeight = Mathf.Max(0f, contentRect.height - RulerHeight - TrackGap);
            float maxStart = Mathf.Max(0f, totalTrackContentHeight - visibleHeight);
            viewStartY = Mathf.Clamp(viewStartY, 0f, maxStart);
        }

        private float TimeToX(float time) => TrackLabelWidth + ContentPadding + (time - viewStartTime) * pixelsPerSecond;

        private float XToTime(float x) => Mathf.Max(0f, viewStartTime + (x - TrackLabelWidth - ContentPadding) / pixelsPerSecond);

        private static float Snap(float time) => Mathf.Round(time / SnapStep) * SnapStep;
    }
}
