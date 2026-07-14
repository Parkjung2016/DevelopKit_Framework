using System;
using System.Collections.Generic;
using System.Reflection;
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
            CustomElementMove,
            CustomElementResizeStart,
            CustomElementResizeEnd,
            BoxSelect
        }

        private enum PendingCreateKind
        {
            Segment,
            Notify,
            NotifyState,
            CustomTrack,
            CustomElement,
            ReplaceSegmentClip,
            ReplaceNotify,
            ReplaceNotifyState
        }

        private enum TrackKind
        {
            Segment,
            Notify,
            NotifyState,
            Custom
        }

        private enum ClipboardContentKind
        {
            None,
            Tracks,
            Elements
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

        private readonly struct CustomElementLayout
        {
            public CustomElementLayout(int index, Rect body)
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

        private readonly struct SegmentClipboardData
        {
            public SegmentClipboardData(
                string sectionName,
                string trackId,
                AnimationClip clip,
                float startTime,
                float clipStartTime,
                float clipEndTime,
                float playRate,
                float blendIn,
                float blendOut,
                Color customColor)
            {
                SectionName = sectionName;
                TrackId = trackId;
                Clip = clip;
                StartTime = startTime;
                ClipStartTime = clipStartTime;
                ClipEndTime = clipEndTime;
                PlayRate = playRate;
                BlendIn = blendIn;
                BlendOut = blendOut;
                CustomColor = customColor;
            }

            public string SectionName { get; }
            public string TrackId { get; }
            public AnimationClip Clip { get; }
            public float StartTime { get; }
            public float ClipStartTime { get; }
            public float ClipEndTime { get; }
            public float PlayRate { get; }
            public float BlendIn { get; }
            public float BlendOut { get; }
            public Color CustomColor { get; }
        }

        private readonly struct NotifyClipboardData
        {
            public NotifyClipboardData(float time, AnimNotify notify, string trackId, float triggerWeightThreshold, Color customColor)
            {
                Time = time;
                Notify = notify;
                TrackId = trackId;
                TriggerWeightThreshold = triggerWeightThreshold;
                CustomColor = customColor;
            }

            public float Time { get; }
            public AnimNotify Notify { get; }
            public string TrackId { get; }
            public float TriggerWeightThreshold { get; }
            public Color CustomColor { get; }
        }

        private readonly struct NotifyStateClipboardData
        {
            public NotifyStateClipboardData(float startTime, float endTime, AnimNotifyState notifyState, string trackId, Color customColor)
            {
                StartTime = startTime;
                EndTime = endTime;
                NotifyState = notifyState;
                TrackId = trackId;
                CustomColor = customColor;
            }

            public float StartTime { get; }
            public float EndTime { get; }
            public AnimNotifyState NotifyState { get; }
            public string TrackId { get; }
            public Color CustomColor { get; }
        }

        private readonly struct CustomElementClipboardData
        {
            public CustomElementClipboardData(float startTime, float endTime, MontageTimelineElement element, string trackId, Color customColor)
            {
                StartTime = startTime;
                EndTime = endTime;
                Element = element;
                TrackId = trackId;
                CustomColor = customColor;
            }

            public float StartTime { get; }
            public float EndTime { get; }
            public MontageTimelineElement Element { get; }
            public string TrackId { get; }
            public Color CustomColor { get; }
        }

        private readonly MontageEditorContext context;
        private readonly List<SegmentLayout> segmentLayouts = new();
        private readonly List<NotifyLayout> notifyLayouts = new();
        private readonly List<NotifyStateLayout> notifyStateLayouts = new();
        private readonly List<CustomElementLayout> customElementLayouts = new();
        private readonly List<TrackRowLayout> trackRows = new();
        private readonly Dictionary<int, float> dragSegmentStartTimes = new();
        private readonly Dictionary<int, float> dragNotifyTimes = new();
        private readonly Dictionary<int, Vector2> dragNotifyStateRanges = new();
        private readonly Dictionary<int, Vector2> dragCustomElementRanges = new();
        private readonly List<TrackIdentity> copiedTracks = new();
        private readonly List<SegmentClipboardData> copiedSegments = new();
        private readonly List<NotifyClipboardData> copiedNotifies = new();
        private readonly List<NotifyStateClipboardData> copiedNotifyStates = new();
        private readonly List<CustomElementClipboardData> copiedCustomElements = new();
        private readonly Dictionary<AudioClip, float[]> audioWaveformCache = new();
        private readonly Label hoverTooltip;

        private float pixelsPerSecond = 120f;
        private float viewStartTime;
        private float viewStartY;
        private float totalTrackContentHeight;

        private DragMode dragMode = DragMode.None;
        private int dragSegmentIndex = -1;
        private int dragNotifyIndex = -1;
        private int dragNotifyStateIndex = -1;
        private int dragCustomElementIndex = -1;
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
        private ClipboardContentKind clipboardKind = ClipboardContentKind.None;
        private Vector2 lastPointerLocal;

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
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            context.Changed += RequestRepaint;
            context.PlayheadChanged += RequestRepaint;
        }

        private void RequestRepaint() => MarkDirtyRepaint();

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

            if (evt.keyCode == KeyCode.Space)
            {
                if (MontageViewportInput.TryInvokePlaybackToggle())
                    evt.StopImmediatePropagation();
                return;
            }

            if (IsEditingLockedInPlayMode())
            {
                evt.StopPropagation();
                return;
            }

            bool actionKey = evt.ctrlKey || evt.commandKey;
            if (actionKey && evt.shiftKey && evt.keyCode == KeyCode.Z)
            {
                Undo.PerformRedo();
                evt.StopPropagation();
                return;
            }

            if (actionKey && evt.keyCode == KeyCode.C)
            {
                if (CopySelectionToClipboard())
                    evt.StopPropagation();
                return;
            }

            if (actionKey && evt.keyCode == KeyCode.V)
            {
                if (PasteClipboard(true))
                    evt.StopPropagation();
                return;
            }

            if (actionKey && evt.keyCode == KeyCode.D)
            {
                if (DuplicateSelection())
                    evt.StopPropagation();
                return;
            }

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.A)
            {
                SelectAllTimelineElements();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            if (!DeleteHoveredTrack() && !DeleteSelected())
                return;

            evt.StopPropagation();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (IsEditingLockedInPlayMode())
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
                return;
            }

            if (CanDropProjectObjects(evt.localMousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (IsEditingLockedInPlayMode())
            {
                evt.StopPropagation();
                return;
            }

            if (!TryDropProjectObjects(evt.localMousePosition))
                return;

            DragAndDrop.AcceptDrag();
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (context.Montage == null)
                return;

            focusController?.IgnoreEvent(evt);
            Focus();
            Vector2 local = evt.localPosition;
            lastPointerLocal = local;

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
                if (!IsEditingLockedInPlayMode())
                    ShowContextMenu(local);
                evt.StopPropagation();
                return;
            }

            if (IsEditingLockedInPlayMode())
            {
                SelectReadonlyTimelineItem(local, evt.shiftKey, evt.ctrlKey || evt.commandKey);
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
                if (!SelectTimelineElementForDrag(
                    dragSegmentIndex,
                    evt.shiftKey,
                    evt.ctrlKey || evt.commandKey,
                    context.IsSegmentSelected,
                    context.SetSelectedSegment))
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
                if (!SelectTimelineElementForDrag(
                    dragNotifyStateIndex,
                    evt.shiftKey,
                    evt.ctrlKey || evt.commandKey,
                    context.IsNotifyStateSelected,
                    context.SetSelectedNotifyState))
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
                if (!SelectTimelineElementForDrag(
                    dragNotifyIndex,
                    evt.shiftKey,
                    evt.ctrlKey || evt.commandKey,
                    context.IsNotifySelected,
                    context.SetSelectedNotify))
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

            if (TryHitCustomElement(local, out int customElementIndex, out DragMode customDrag))
            {
                if (!SelectTimelineElementForDrag(
                    customElementIndex,
                    evt.shiftKey,
                    evt.ctrlKey || evt.commandKey,
                    context.IsCustomElementSelected,
                    context.SetSelectedCustomElement))
                {
                    evt.StopPropagation();
                    return;
                }

                dragCustomElementIndex = customElementIndex;
                BeginDrag(customDrag, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                CustomMontageElementPlacement element = context.Montage.CustomElements[customElementIndex];
                dragAnchorValue = customDrag == DragMode.CustomElementResizeEnd ? element.EndTime : element.StartTime;
                CaptureDragSelectionAnchors();
                evt.StopPropagation();
                return;
            }

            if (evt.clickCount == 2 && TryCreateElementByDoubleClick(local))
            {
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

            BeginDrag(DragMode.BoxSelect, evt.pointerId);
            boxSelectStart = local;
            boxSelectEnd = local;
            boxSelectAdditive = evt.shiftKey || evt.ctrlKey || evt.commandKey;
            HideHoverTooltip();
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            lastPointerLocal = evt.localPosition;
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
                case DragMode.CustomElementMove when dragCustomElementIndex >= 0:
                    ApplySelectedTimelineMove(time - dragAnchorTime, evt.localPosition, TrackKind.Custom);
                    break;

                case DragMode.CustomElementResizeStart when dragCustomElementIndex >= 0:
                    ApplyCustomElementRange(
                        dragCustomElementIndex,
                        time,
                        context.Montage.CustomElements[dragCustomElementIndex].EndTime);
                    break;

                case DragMode.CustomElementResizeEnd when dragCustomElementIndex >= 0:
                    ApplyCustomElementRange(
                        dragCustomElementIndex,
                        context.Montage.CustomElements[dragCustomElementIndex].StartTime,
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
            dragCustomElementIndex = -1;
            hasSnapGuide = false;
            if (endedDragMode == DragMode.BoxSelect)
                ApplyBoxSelection();

            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void BeginDrag(DragMode mode, int pointerId)
        {
            dragMode = mode;
            HideHoverTooltip();
            this.CapturePointer(pointerId);
        }

        private void CaptureDragSelectionAnchors()
        {
            dragSegmentStartTimes.Clear();
            dragNotifyTimes.Clear();
            dragNotifyStateRanges.Clear();
            dragCustomElementRanges.Clear();

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
            foreach (int index in context.SelectedCustomElementIndices)
            {
                if (index < 0 || index >= montage.CustomElements.Count || montage.CustomElements[index] == null)
                    continue;

                CustomMontageElementPlacement element = montage.CustomElements[index];
                dragCustomElementRanges[index] = new Vector2(element.StartTime, element.EndTime);
            }
        }

        private static bool IsEditingLockedInPlayMode() => EditorApplication.isPlaying;

        private void SelectReadonlyTimelineItem(Vector2 local, bool additive, bool toggle)
        {
            if (TryGetTrackRow(local, out TrackRowLayout row) && IsTrackLabel(local, row))
            {
                context.SetSelectedTimelineTrack(GetTrackKey(row.Kind, row.TrackId), additive || toggle, toggle);
                return;
            }

            if (TryHitSegment(local, out int segmentIndex, out _))
            {
                context.SetSelectedSegment(segmentIndex, additive, toggle);
                return;
            }

            if (TryHitNotifyState(local, out int stateIndex, out _))
            {
                context.SetSelectedNotifyState(stateIndex, additive, toggle);
                return;
            }

            if (TryHitNotify(local, out int notifyIndex))
            {
                context.SetSelectedNotify(notifyIndex, additive, toggle);
                return;
            }

            if (TryHitCustomElement(local, out int customIndex, out _))
                context.SetSelectedCustomElement(customIndex, additive, toggle);
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
                if (Selection.activeObject is AnimationClip replacementClip && IsCompatibleAnimationClip(replacementClip))
                    menu.AddItem(new GUIContent("Segment/Replace Clip From Project Selection"), false, () => ReplaceSegmentClip(segmentIndex, replacementClip));
                else
                    menu.AddDisabledItem(new GUIContent("Segment/Replace Clip From Project Selection"));
                if (CanSplitSegmentAtTime(segmentIndex, time))
                    menu.AddItem(new GUIContent("Segment/Split At Cursor"), false, () => SplitSegmentAtTime(segmentIndex, time));
                else
                    menu.AddDisabledItem(new GUIContent("Segment/Split At Cursor"));
                menu.AddItem(new GUIContent("Segment/Reset Trim"), false, () => ResetSegmentTrim(segmentIndex));
                AddElementColorMenuItems(menu, TrackKind.Segment, segmentIndex);
                menu.AddItem(new GUIContent("Segment/Delete"), false, () => DeleteArrayElement("segments", segmentIndex, "Delete Montage Segment"));
                menu.AddItem(new GUIContent("Segment/Select"), false, () => context.SetSelectedSegment(segmentIndex));
                menu.AddSeparator("");
            }

            if (TryHitNotify(local, out int notifyIndex))
            {
                menu.AddItem(new GUIContent("Notify/Replace Notify..."), false, () => OpenCreatePicker(PendingCreateKind.ReplaceNotify, time, "Default", notifyIndex));
                AddElementColorMenuItems(menu, TrackKind.Notify, notifyIndex);
                menu.AddItem(new GUIContent("Notify/Delete"), false, () => DeleteArrayElement("notifies", notifyIndex, "Delete Anim Notify"));
                menu.AddSeparator("");
            }

            if (TryHitNotifyState(local, out int notifyStateIndex, out _))
            {
                menu.AddItem(new GUIContent("Notify State/Replace Notify State..."), false, () => OpenCreatePicker(PendingCreateKind.ReplaceNotifyState, time, "Default", notifyStateIndex));
                AddElementColorMenuItems(menu, TrackKind.NotifyState, notifyStateIndex);
                menu.AddItem(new GUIContent("Notify State/Delete"), false, () => DeleteArrayElement("notifyStates", notifyStateIndex, "Delete Anim Notify State"));
                menu.AddSeparator("");
            }

            if (TryHitCustomElement(local, out int customElementIndex))
            {
                menu.AddItem(new GUIContent("Custom Element/Delete"), false, () => DeleteArrayElement("customElements", customElementIndex, "Delete Custom Montage Element"));
                menu.AddItem(new GUIContent("Custom Element/Select"), false, () => context.SetSelectedCustomElement(customElementIndex));
                menu.AddSeparator("");
            }

            if (hasRow && row.Kind == TrackKind.Segment)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Animation Segment..."), false, () => OpenCreatePicker(PendingCreateKind.Segment, time, trackId));
                if (Selection.activeObject is AnimationClip selectedClip && IsCompatibleAnimationClip(selectedClip))
                    menu.AddItem(new GUIContent("Create/Segment From Project Selection"), false, () => AddSegmentAtTime(time, selectedClip, trackId));
                else
                    menu.AddDisabledItem(new GUIContent("Create/Segment From Project Selection"));
            }
            else if (hasRow && row.Kind == TrackKind.Notify)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Notify..."), false, () => OpenCreatePicker(PendingCreateKind.Notify, time, trackId));
            }
            else if (hasRow && row.Kind == TrackKind.NotifyState)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Notify State..."), false, () => OpenCreatePicker(PendingCreateKind.NotifyState, time, trackId));
            }
            else if (hasRow && row.Kind == TrackKind.Custom)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Element..."), false, () => OpenCreatePicker(PendingCreateKind.CustomElement, time, trackId));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Create"));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Track/Add Animation Track"), false, () => AddTrack("animationTracks", "Animation"));
            menu.AddItem(new GUIContent("Track/Add Notify Track"), false, () => AddTrack("notifyTracks", "Notify"));
            menu.AddItem(new GUIContent("Track/Add Notify State Track"), false, () => AddTrack("notifyStateTracks", "Notify State"));
            menu.AddItem(new GUIContent("Track/Add Custom Track..."), false, () => OpenCreatePicker(PendingCreateKind.CustomTrack, time, "Default"));

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

        private bool TryCreateElementByDoubleClick(Vector2 local)
        {
            if (!TryGetTrackRow(local, out TrackRowLayout row))
                return false;

            float time = Snap(XToTime(local.x));
            string trackId = row.TrackId;
            switch (row.Kind)
            {
                case TrackKind.Segment:
                    if (Selection.activeObject is AnimationClip selectedClip && IsCompatibleAnimationClip(selectedClip))
                        AddSegmentAtTime(time, selectedClip, trackId);
                    else
                        OpenCreatePicker(PendingCreateKind.Segment, time, trackId);
                    return true;

                case TrackKind.Notify:
                    OpenCreatePicker(PendingCreateKind.Notify, time, trackId);
                    return true;

                case TrackKind.NotifyState:
                    OpenCreatePicker(PendingCreateKind.NotifyState, time, trackId);
                    return true;

                case TrackKind.Custom:
                    OpenCreatePicker(PendingCreateKind.CustomElement, time, trackId);
                    return true;

                default:
                    return false;
            }
        }

        private bool CanDropProjectObjects(Vector2 local)
        {
            if (context.Montage == null || !TryGetTrackRow(local, TrackKind.Segment, out _))
                return false;

            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is AnimationClip clip && IsCompatibleAnimationClip(clip))
                    return true;
            }

            return false;
        }

        private bool TryDropProjectObjects(Vector2 local)
        {
            if (context.Montage == null || !TryGetTrackRow(local, TrackKind.Segment, out TrackRowLayout row))
                return false;

            float time = Snap(XToTime(local.x));
            bool added = false;
            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is not AnimationClip clip || !IsCompatibleAnimationClip(clip))
                    continue;

                AddSegmentAtTime(time, clip, row.TrackId);
                time = Snap(time + Mathf.Max(MinSegmentDuration, clip.length));
                added = true;
            }

            return added;
        }

        private void AddElementColorMenuItems(GenericMenu menu, TrackKind kind, int index)
        {
            string prefix = kind switch
            {
                TrackKind.Segment => "Segment/Color",
                TrackKind.Notify => "Notify/Color",
                TrackKind.NotifyState => "Notify State/Color",
                _ => "Color"
            };

            menu.AddItem(new GUIContent($"{prefix}/Reset"), false, () => ApplyElementColor(kind, index, Color.clear));
            menu.AddItem(new GUIContent($"{prefix}/Blue"), false, () => ApplyElementColor(kind, index, new Color(0.32f, 0.58f, 1f, 0.95f)));
            menu.AddItem(new GUIContent($"{prefix}/Green"), false, () => ApplyElementColor(kind, index, new Color(0.34f, 0.86f, 0.48f, 0.95f)));
            menu.AddItem(new GUIContent($"{prefix}/Orange"), false, () => ApplyElementColor(kind, index, new Color(1f, 0.58f, 0.22f, 0.95f)));
            menu.AddItem(new GUIContent($"{prefix}/Pink"), false, () => ApplyElementColor(kind, index, new Color(1f, 0.42f, 0.68f, 0.95f)));
            menu.AddItem(new GUIContent($"{prefix}/Purple"), false, () => ApplyElementColor(kind, index, new Color(0.72f, 0.46f, 1f, 0.95f)));
        }

        private void ApplyElementColor(TrackKind kind, int clickedIndex, Color color)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || clickedIndex < 0)
                return;

            string propertyName = kind switch
            {
                TrackKind.Segment => "segments",
                TrackKind.Notify => "notifies",
                TrackKind.NotifyState => "notifyStates",
                TrackKind.Custom => "customElements",
                _ => null
            };

            if (string.IsNullOrEmpty(propertyName))
                return;

            IReadOnlyCollection<int> targets = GetColorTargets(kind, clickedIndex);
            Undo.RecordObject(montage, "Set Montage Element Color");
            SerializedObject so = new(montage);
            SerializedProperty items = so.FindProperty(propertyName);
            if (items == null)
                return;

            foreach (int index in targets)
            {
                if (index < 0 || index >= items.arraySize)
                    continue;

                SerializedProperty colorProperty = items.GetArrayElementAtIndex(index).FindPropertyRelative("customColor");
                if (colorProperty != null)
                    colorProperty.colorValue = color;
            }

            so.ApplyModifiedProperties();
            context.MarkDirty();
            MarkDirtyRepaint();
        }

        private static bool SelectTimelineElementForDrag(
            int index,
            bool additive,
            bool toggle,
            Func<int, bool> isSelected,
            Action<int, bool, bool> setSelected)
        {
            if (!additive && !toggle && isSelected(index))
                return true;

            setSelected(index, additive, toggle);
            return !toggle || isSelected(index);
        }

        private IReadOnlyCollection<int> GetColorTargets(TrackKind kind, int clickedIndex)
        {
            return kind switch
            {
                TrackKind.Segment when context.IsSegmentSelected(clickedIndex) => context.SelectedSegmentIndices,
                TrackKind.Notify when context.IsNotifySelected(clickedIndex) => context.SelectedNotifyIndices,
                TrackKind.NotifyState when context.IsNotifyStateSelected(clickedIndex) => context.SelectedNotifyStateIndices,
                TrackKind.Custom when context.IsCustomElementSelected(clickedIndex) => context.SelectedCustomElementIndices,
                _ => new[] { clickedIndex }
            };
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
                    MontageObjectPickerWindow.Show<AnimationClip>(
                        "Create Animation Segment",
                        clip => AddSegmentAtTime(time, clip, trackId),
                        IsCompatibleAnimationClip);
                    break;

                case PendingCreateKind.ReplaceSegmentClip:
                    MontageObjectPickerWindow.Show<AnimationClip>(
                        "Replace Animation Clip",
                        clip => ReplaceSegmentClip(editIndex, clip),
                        IsCompatibleAnimationClip);
                    break;

                case PendingCreateKind.Notify:
                    MontageTypePickerWindow.Show<AnimNotify>("Create Notify", notify => AddNotifyAtTime(time, notify, trackId));
                    break;

                case PendingCreateKind.ReplaceNotify:
                    MontageTypePickerWindow.Show<AnimNotify>("Replace Notify", notify => ReplaceNotify(editIndex, notify));
                    break;

                case PendingCreateKind.NotifyState:
                    MontageTypePickerWindow.Show<AnimNotifyState>("Create Notify State", state => AddNotifyStateAtTime(time, state, trackId));
                    break;

                case PendingCreateKind.ReplaceNotifyState:
                    MontageTypePickerWindow.Show<AnimNotifyState>("Replace Notify State", state => ReplaceNotifyState(editIndex, state));
                    break;

                case PendingCreateKind.CustomTrack:
                    MontageTypePickerWindow.Show<MontageTimelineTrack>("Create Custom Track", AddCustomTrack);
                    break;

                case PendingCreateKind.CustomElement:
                    MontageTypePickerWindow.Show<MontageTimelineElement>(
                        "Create Custom Element",
                        element => AddCustomElementAtTime(time, element, trackId),
                        type => CanElementTypeAttachToTrack(type, context.Montage, trackId));
                    break;
            }
        }

        private static bool CanElementTypeAttachToTrack(Type elementType, AnimMontageSO montage, string trackId)
        {
            if (elementType == null || elementType.IsAbstract || elementType.GetConstructor(Type.EmptyTypes) == null)
                return false;

            return Activator.CreateInstance(elementType) is MontageTimelineElement element
                && CanElementAttachToTrack(element, montage, trackId);
        }

        private static bool CanElementAttachToTrack(MontageTimelineElement element, AnimMontageSO montage, string trackId)
        {
            if (element == null || montage == null)
                return false;

            CustomMontageTrack track = FindCustomTrack(montage, trackId);
            return element.CanAttachToTrack(track?.TrackType);
        }

        private bool IsCompatibleAnimationClip(AnimationClip clip) =>
            MontageAnimationClipCompatibility.IsCompatible(context.PreviewModel, clip);

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

            deltaTime = ClampSelectedTimelineMoveDelta(deltaTime, montage);

            string targetTrackId = null;
            bool allowTrackTransfer = dragSegmentStartTimes.Count + dragNotifyTimes.Count + dragNotifyStateRanges.Count + dragCustomElementRanges.Count <= 1;
            if (allowTrackTransfer && TryGetTrackRow(local, activeKind, out TrackRowLayout targetRow))
                targetTrackId = targetRow.TrackId;

            Undo.RecordObject(montage, "Move Montage Timeline Elements");
            SerializedObject so = new(montage);

            SerializedProperty segments = so.FindProperty("segments");
            foreach (KeyValuePair<int, float> entry in dragSegmentStartTimes)
            {
                if (segments == null || entry.Key < 0 || entry.Key >= segments.arraySize)
                    continue;

                MontageSegment montageSegment = entry.Key < montage.Segments.Count ? montage.Segments[entry.Key] : null;
                float duration = montageSegment?.Duration ?? 0f;
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
            if (activeKind == TrackKind.Custom)
            {
                SerializedProperty customElements = so.FindProperty("customElements");
                foreach (KeyValuePair<int, Vector2> entry in dragCustomElementRanges)
                {
                    if (customElements == null || entry.Key < 0 || entry.Key >= customElements.arraySize)
                        continue;

                    float duration = Mathf.Max(MinSegmentDuration, entry.Value.y - entry.Value.x);
                    float startTime = Snap(Mathf.Max(0f, entry.Value.x + deltaTime));
                    float endTime = Snap(startTime + duration);
                    SerializedProperty element = customElements.GetArrayElementAtIndex(entry.Key);
                    element.FindPropertyRelative("startTime").floatValue = startTime;
                    element.FindPropertyRelative("endTime").floatValue = endTime;
                    if (targetTrackId != null)
                        element.FindPropertyRelative("trackId").stringValue = targetTrackId;
                }
            }

            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private float ClampSelectedTimelineMoveDelta(float deltaTime, AnimMontageSO montage)
        {
            if (montage == null)
                return deltaTime;

            float minTime = float.PositiveInfinity;
            float maxTime = float.NegativeInfinity;

            foreach (KeyValuePair<int, float> entry in dragSegmentStartTimes)
            {
                if (entry.Key < 0 || entry.Key >= montage.Segments.Count)
                    continue;

                MontageSegment segment = montage.Segments[entry.Key];
                if (segment == null)
                    continue;

                minTime = Mathf.Min(minTime, entry.Value);
                maxTime = Mathf.Max(maxTime, entry.Value + segment.Duration);
            }

            foreach (KeyValuePair<int, float> entry in dragNotifyTimes)
            {
                minTime = Mathf.Min(minTime, entry.Value);
                maxTime = Mathf.Max(maxTime, entry.Value);
            }

            foreach (KeyValuePair<int, Vector2> entry in dragNotifyStateRanges)
            {
                minTime = Mathf.Min(minTime, entry.Value.x);
                maxTime = Mathf.Max(maxTime, entry.Value.y);
            }
            foreach (KeyValuePair<int, Vector2> entry in dragCustomElementRanges)
            {
                minTime = Mathf.Min(minTime, entry.Value.x);
                maxTime = Mathf.Max(maxTime, entry.Value.y);
            }

            if (float.IsPositiveInfinity(minTime) || float.IsNegativeInfinity(maxTime))
                return deltaTime;

            return Mathf.Max(deltaTime, -minTime);
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
            startTime = SnapTimelineEdgeTime(
                Mathf.Clamp(startTime, 0f, maxStartTime),
                TrackKind.Segment,
                segment.TrackId,
                ignoreSegmentIndex: segmentIndex);
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
            float maxEndTime = segment.IsLoopingClip
                ? float.PositiveInfinity
                : segment.StartTime + (segment.Clip.length - dragAnchorClipStart) / playRate;
            endTime = SnapTimelineEdgeTime(
                segment.IsLoopingClip
                    ? Mathf.Max(minEndTime, endTime)
                    : Mathf.Clamp(endTime, minEndTime, maxEndTime),
                TrackKind.Segment,
                segment.TrackId,
                ignoreSegmentIndex: segmentIndex);
            float clipEnd = dragAnchorClipStart + (endTime - segment.StartTime) * playRate;
            if (!segment.IsLoopingClip)
                clipEnd = Mathf.Clamp(clipEnd, dragAnchorClipStart + MinSegmentDuration * playRate, segment.Clip.length);
            else
                clipEnd = Mathf.Max(dragAnchorClipStart + MinSegmentDuration * playRate, clipEnd);

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

        private float SnapTimelineEdgeTime(
            float time,
            TrackKind kind,
            string trackId,
            int ignoreSegmentIndex = -1,
            int ignoreNotifyStateIndex = -1,
            int ignoreCustomElementIndex = -1)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return Snap(time);

            trackId = SanitizeTrackId(trackId);
            float tolerance = MagneticSnapPixels / Mathf.Max(1f, pixelsPerSecond);
            float bestDistance = tolerance;
            float snappedTime = time;
            bool snapped = false;

            TrySnapToGuide(0f);

            if (kind == TrackKind.Segment)
            {
                for (int i = 0; i < montage.Segments.Count; i++)
                {
                    if (i == ignoreSegmentIndex)
                        continue;

                    MontageSegment segment = montage.Segments[i];
                    if (segment == null || segment.TrackId != trackId)
                        continue;

                    TrySnapToGuide(segment.StartTime);
                    TrySnapToGuide(segment.EndTime);
                }
            }
            else if (kind == TrackKind.NotifyState)
            {
                for (int i = 0; i < montage.NotifyStates.Count; i++)
                {
                    if (i == ignoreNotifyStateIndex)
                        continue;

                    AnimNotifyStatePlacement placement = montage.NotifyStates[i];
                    if (placement == null || placement.TrackId != trackId)
                        continue;

                    TrySnapToGuide(placement.StartTime);
                    TrySnapToGuide(placement.EndTime);
                }
            }            else if (kind == TrackKind.Custom)
            {
                for (int i = 0; i < montage.CustomElements.Count; i++)
                {
                    if (i == ignoreCustomElementIndex)
                        continue;

                    CustomMontageElementPlacement placement = montage.CustomElements[i];
                    if (placement == null || placement.TrackId != trackId)
                        continue;

                    TrySnapToGuide(placement.StartTime);
                    TrySnapToGuide(placement.EndTime);
                }
            }

            if (!snapped)
                return Snap(time);

            hasSnapGuide = true;
            snapGuideTime = snappedTime;
            return Snap(Mathf.Max(0f, snappedTime));

            void TrySnapToGuide(float guideTime)
            {
                float distance = Mathf.Abs(time - guideTime);
                if (distance > bestDistance)
                    return;

                bestDistance = distance;
                snappedTime = guideTime;
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
            AnimNotifyStatePlacement placement = context.Montage.NotifyStates[notifyStateIndex];
            string trackId = placement != null ? placement.TrackId : "Default";
            if (dragMode == DragMode.NotifyStateResizeStart)
            {
                float fixedEndTime = Snap(Mathf.Max(0f, endTime));
                startTime = SnapTimelineEdgeTime(
                    Mathf.Clamp(startTime, 0f, fixedEndTime - MinSegmentDuration),
                    TrackKind.NotifyState,
                    trackId,
                    ignoreNotifyStateIndex: notifyStateIndex);
                startTime = Snap(Mathf.Min(startTime, fixedEndTime - MinSegmentDuration));
                endTime = fixedEndTime;
            }
            else if (dragMode == DragMode.NotifyStateResizeEnd)
            {
                startTime = Snap(Mathf.Max(0f, startTime));
                endTime = SnapTimelineEdgeTime(
                    Mathf.Max(startTime + MinSegmentDuration, endTime),
                    TrackKind.NotifyState,
                    trackId,
                    ignoreNotifyStateIndex: notifyStateIndex);
            }
            else
            {
                startTime = Snap(Mathf.Max(0f, startTime));
                endTime = Snap(Mathf.Max(startTime + MinSegmentDuration, endTime));
            }

            Undo.RecordObject(context.Montage, "Adjust Notify State");
            placement.StartTime = startTime;
            placement.EndTime = endTime;
            context.MarkDirty();
        }

        private void ApplyCustomElementRange(int customElementIndex, float startTime, float endTime)
        {
            if (context.Montage == null
                || customElementIndex < 0
                || customElementIndex >= context.Montage.CustomElements.Count)
                return;

            CustomMontageElementPlacement placement = context.Montage.CustomElements[customElementIndex];
            if (placement == null)
                return;

            string trackId = placement.TrackId;
            if (dragMode == DragMode.CustomElementResizeStart)
            {
                float fixedEndTime = Snap(Mathf.Max(0f, endTime));
                startTime = SnapTimelineEdgeTime(
                    Mathf.Clamp(startTime, 0f, fixedEndTime - MinSegmentDuration),
                    TrackKind.Custom,
                    trackId,
                    ignoreCustomElementIndex: customElementIndex);
                startTime = Snap(Mathf.Min(startTime, fixedEndTime - MinSegmentDuration));
                endTime = fixedEndTime;
            }
            else if (dragMode == DragMode.CustomElementResizeEnd)
            {
                startTime = Snap(Mathf.Max(0f, startTime));
                endTime = SnapTimelineEdgeTime(
                    Mathf.Max(startTime + MinSegmentDuration, endTime),
                    TrackKind.Custom,
                    trackId,
                    ignoreCustomElementIndex: customElementIndex);
            }
            else
            {
                startTime = Snap(Mathf.Max(0f, startTime));
                endTime = Snap(Mathf.Max(startTime + MinSegmentDuration, endTime));
            }

            Undo.RecordObject(context.Montage, "Adjust Custom Montage Element");
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

        private void AddNotifyAtTime(float time, AnimNotify notify) => AddNotifyAtTime(time, notify, "Default");

        private void AddNotifyAtTime(float time, AnimNotify notify, string trackId)
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
            element.FindPropertyRelative("notify").managedReferenceValue = notify;
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            element.FindPropertyRelative("customColor").colorValue = Color.clear;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedNotify(index);
        }

        private void AddNotifyStateAtTime(float time) => AddNotifyStateAtTime(time, null, "Default");

        private void AddNotifyStateAtTime(float time, AnimNotifyState notifyState) => AddNotifyStateAtTime(time, notifyState, "Default");

        private void AddNotifyStateAtTime(float time, AnimNotifyState notifyState, string trackId)
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
            element.FindPropertyRelative("notifyState").managedReferenceValue = notifyState;
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            element.FindPropertyRelative("customColor").colorValue = Color.clear;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedNotifyState(index);
        }

        private void AddCustomElementAtTime(float time, MontageTimelineElement elementAsset, string trackId)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            trackId = SanitizeTrackId(trackId);
            if (!CanElementAttachToTrack(elementAsset, montage, trackId))
            {
                string elementName = elementAsset != null ? elementAsset.DisplayName : "Custom Element";
                Debug.LogWarning($"[AnimMontage] '{elementName}' cannot be added to '{trackId}' track.", montage);
                return;
            }

            float duration = Mathf.Max(MinSegmentDuration, elementAsset != null ? elementAsset.DefaultDuration : DefaultQuickBlendDuration);
            Undo.RecordObject(montage, "Add Custom Montage Element");
            SerializedObject so = new(montage);
            SerializedProperty prop = so.FindProperty("customElements");
            int index = prop.arraySize;
            prop.InsertArrayElementAtIndex(index);
            SerializedProperty element = prop.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("startTime").floatValue = Snap(time);
            element.FindPropertyRelative("endTime").floatValue = Snap(time + duration);
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            element.FindPropertyRelative("element").managedReferenceValue = elementAsset;
            element.FindPropertyRelative("customColor").colorValue = Color.clear;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelected(montage);
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
            element.FindPropertyRelative("customColor").colorValue = Color.clear;
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

        public bool HasSelectedSegment() =>
            context.Montage != null
            && context.SelectedSegmentIndex >= 0
            && context.SelectedSegmentIndex < context.Montage.Segments.Count;

        public bool CanSplitSelectedSegmentAtPlayhead() =>
            HasSelectedSegment() && CanSplitSegmentAtTime(context.SelectedSegmentIndex, context.PlayheadTime);

        public void SplitSelectedSegmentAtPlayhead()
        {
            if (CanSplitSelectedSegmentAtPlayhead())
                SplitSegmentAtTime(context.SelectedSegmentIndex, context.PlayheadTime);
        }

        public void ReplaceSelectedSegmentClip()
        {
            if (!HasSelectedSegment())
                return;

            OpenCreatePicker(PendingCreateKind.ReplaceSegmentClip, context.PlayheadTime, "Default", context.SelectedSegmentIndex);
        }

        public void ResetSelectedSegmentTrim()
        {
            if (HasSelectedSegment())
                ResetSegmentTrim(context.SelectedSegmentIndex);
        }
        private bool CanSplitSegmentAtTime(int segmentIndex, float time)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || segmentIndex < 0 || segmentIndex >= montage.Segments.Count)
                return false;

            MontageSegment segment = montage.Segments[segmentIndex];
            if (segment?.Clip == null)
                return false;

            if (segment.IsLoopingClip)
                return false;

            return time > segment.StartTime + MinSegmentDuration && time < segment.EndTime - MinSegmentDuration;
        }

        private void SplitSegmentAtTime(int segmentIndex, float time)
        {
            AnimMontageSO montage = context.Montage;
            if (!CanSplitSegmentAtTime(segmentIndex, time))
                return;

            MontageSegment source = montage.Segments[segmentIndex];
            float splitTime = Snap(Mathf.Clamp(time, source.StartTime + MinSegmentDuration, source.EndTime - MinSegmentDuration));
            float splitClipTime = source.ToPlayableClipTime(splitTime);
            float sourceClipEndTime = source.ClipEndTime;
            if (splitClipTime <= source.ClipStartTime + 0.0001f || splitClipTime >= sourceClipEndTime - 0.0001f)
                return;

            float secondClipStartTime = source.IsLoopingClip ? source.NormalizeClipTime(splitClipTime) : splitClipTime;
            float secondClipEndTime = source.IsLoopingClip
                ? secondClipStartTime + Mathf.Max(0f, sourceClipEndTime - splitClipTime)
                : sourceClipEndTime;
            if (secondClipEndTime <= secondClipStartTime + 0.0001f)
                return;

            Undo.RecordObject(montage, "Split Montage Segment");
            SerializedObject so = new(montage);
            SerializedProperty segments = so.FindProperty("segments");
            SerializedProperty first = segments.GetArrayElementAtIndex(segmentIndex);
            int secondIndex = segmentIndex + 1;
            segments.InsertArrayElementAtIndex(secondIndex);
            SerializedProperty second = segments.GetArrayElementAtIndex(secondIndex);

            CopySegmentProperty(first, second);
            first.FindPropertyRelative("clipEndTime").floatValue = splitClipTime;
            second.FindPropertyRelative("startTime").floatValue = splitTime;
            second.FindPropertyRelative("clipStartTime").floatValue = secondClipStartTime;
            second.FindPropertyRelative("clipEndTime").floatValue = secondClipEndTime;

            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedSegment(secondIndex);
            context.SetPlayhead(splitTime);
            MarkDirtyRepaint();
        }

        private static void CopySegmentProperty(SerializedProperty source, SerializedProperty target)
        {
            target.FindPropertyRelative("sectionName").stringValue = source.FindPropertyRelative("sectionName").stringValue;
            target.FindPropertyRelative("trackId").stringValue = source.FindPropertyRelative("trackId").stringValue;
            target.FindPropertyRelative("clip").objectReferenceValue = source.FindPropertyRelative("clip").objectReferenceValue;
            target.FindPropertyRelative("startTime").floatValue = source.FindPropertyRelative("startTime").floatValue;
            target.FindPropertyRelative("clipStartTime").floatValue = source.FindPropertyRelative("clipStartTime").floatValue;
            target.FindPropertyRelative("clipEndTime").floatValue = source.FindPropertyRelative("clipEndTime").floatValue;
            target.FindPropertyRelative("playRate").floatValue = source.FindPropertyRelative("playRate").floatValue;
            target.FindPropertyRelative("blendIn").floatValue = source.FindPropertyRelative("blendIn").floatValue;
            target.FindPropertyRelative("blendOut").floatValue = source.FindPropertyRelative("blendOut").floatValue;
            target.FindPropertyRelative("customColor").colorValue = source.FindPropertyRelative("customColor").colorValue;
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

        private void ReplaceNotify(int notifyIndex, AnimNotify notify)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || notifyIndex < 0 || notifyIndex >= montage.Notifies.Count)
                return;

            Undo.RecordObject(montage, "Replace Anim Notify");
            SerializedObject so = new(montage);
            SerializedProperty placement = so.FindProperty("notifies").GetArrayElementAtIndex(notifyIndex);
            placement.FindPropertyRelative("notify").managedReferenceValue = notify;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedNotify(notifyIndex);
        }

        private void ReplaceNotifyState(int notifyStateIndex, AnimNotifyState notifyState)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || notifyStateIndex < 0 || notifyStateIndex >= montage.NotifyStates.Count)
                return;

            Undo.RecordObject(montage, "Replace Anim Notify State");
            SerializedObject so = new(montage);
            SerializedProperty placement = so.FindProperty("notifyStates").GetArrayElementAtIndex(notifyStateIndex);
            placement.FindPropertyRelative("notifyState").managedReferenceValue = notifyState;
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
                || context.SelectedNotifyStateIndices.Count > 0
                || context.SelectedCustomElementIndices.Count > 0)
                return DeleteSelectedTimelineElements();

            if (context.SelectedTimelineTrackKeys.Count > 0)
                return DeleteSelectedTimelineTracks();

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
            DeleteArrayElementsWithoutApply(so.FindProperty("customElements"), context.SelectedCustomElementIndices);
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelected(montage);
            return true;
        }

        private bool DeleteSelectedTimelineTracks()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || context.SelectedTimelineTrackKeys.Count == 0)
                return false;

            var tracksToDelete = new List<TrackIdentity>();
            foreach (string key in context.SelectedTimelineTrackKeys)
            {
                if (!TryParseTrackKey(key, out TrackIdentity identity) || identity.TrackId == "Default")
                    continue;

                tracksToDelete.Add(identity);
            }

            if (tracksToDelete.Count == 0)
                return false;

            int itemCount = 0;
            for (int i = 0; i < tracksToDelete.Count; i++)
                itemCount += CountTrackItems(montage, tracksToDelete[i].Kind, tracksToDelete[i].TrackId);

            string message = itemCount > 0
                ? $"Delete {tracksToDelete.Count} track(s) and {itemCount} item(s) on them?"
                : $"Delete {tracksToDelete.Count} track(s)?";
            if (!EditorUtility.DisplayDialog("Delete Montage Track", message, "Delete", "Cancel"))
                return false;

            Undo.RecordObject(montage, "Delete Montage Track");
            SerializedObject so = new(montage);
            SerializedProperty order = so.FindProperty("timelineTrackOrder");
            for (int i = 0; i < tracksToDelete.Count; i++)
            {
                TrackIdentity track = tracksToDelete[i];
                DeleteTrackItems(so, track.Kind, track.TrackId);

                SerializedProperty tracks = so.FindProperty(GetTrackPropertyName(track.Kind));
                if (tracks != null)
                {
                    for (int j = tracks.arraySize - 1; j >= 0; j--)
                    {
                        SerializedProperty trackProperty = tracks.GetArrayElementAtIndex(j);
                        SerializedProperty customTrackId = trackProperty.FindPropertyRelative("trackId");
                        string currentTrackId = customTrackId != null
                            ? customTrackId.stringValue
                            : trackProperty.stringValue;
                        if (SanitizeTrackId(currentTrackId) == track.TrackId)
                            tracks.DeleteArrayElementAtIndex(j);
                    }
                }

                if (order != null)
                    RemoveTrackKeyFromOrder(order, GetTrackKey(track.Kind, track.TrackId));
            }

            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelected(montage);
            MarkDirtyRepaint();
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

        private void AddCustomTrack(MontageTimelineTrack trackType)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Add Custom Montage Track");
            SerializedObject so = new(montage);
            SerializedProperty tracks = so.FindProperty("customTracks");
            if (tracks == null)
                return;

            string displayName = trackType != null ? trackType.DisplayName : "Custom";
            string trackId = CreateUniqueCustomTrackId(tracks, displayName);
            int index = tracks.arraySize;
            tracks.InsertArrayElementAtIndex(index);
            SerializedProperty track = tracks.GetArrayElementAtIndex(index);
            track.FindPropertyRelative("trackId").stringValue = trackId;
            track.FindPropertyRelative("trackType").managedReferenceValue = trackType;
            track.FindPropertyRelative("customColor").colorValue = Color.clear;

            SerializedProperty order = so.FindProperty("timelineTrackOrder");
            if (order != null)
                EnsureTrackKeyInOrder(order, GetTrackKey(TrackKind.Custom, trackId));

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
                    SerializedProperty track = tracks.GetArrayElementAtIndex(i);
                    SerializedProperty customTrackId = track.FindPropertyRelative("trackId");
                    string currentTrackId = customTrackId != null
                        ? customTrackId.stringValue
                        : track.stringValue;
                    if (SanitizeTrackId(currentTrackId) == trackId)
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
            if (sourceKind != TrackKind.Custom)
                EnsureTrackInProperty(so.FindProperty(GetTrackPropertyName(sourceKind)), sourceTrackId);
            if (targetKind != TrackKind.Custom)
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
            {
                if (!boxSelectAdditive)
                    context.SetSelected(context.Montage);

                return;
            }

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

            var customElementSelection = new List<int>();
            for (int i = 0; i < customElementLayouts.Count; i++)
            {
                CustomElementLayout layout = customElementLayouts[i];
                if (layout.Body.Overlaps(selectionRect))
                    customElementSelection.Add(layout.Index);
            }

            var trackSelection = new List<string>();
            for (int i = 0; i < trackRows.Count; i++)
            {
                TrackRowLayout row = trackRows[i];
                var labelRect = new Rect(row.Rect.xMin, row.Rect.yMin, TrackLabelWidth, row.Rect.height);
                if (labelRect.Overlaps(selectionRect))
                    trackSelection.Add(GetTrackKey(row.Kind, row.TrackId));
            }

            if (segmentSelection.Count > 0
                || notifySelection.Count > 0
                || stateSelection.Count > 0
                || customElementSelection.Count > 0
                || trackSelection.Count > 0)
                context.SetSelectedTimelineElements(segmentSelection, notifySelection, stateSelection, customElementSelection, trackSelection, boxSelectAdditive);
            else if (!boxSelectAdditive)
                context.SetSelected(context.Montage);
        }

        private void SelectAllTimelineElements()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            var segmentSelection = new List<int>();
            for (int i = 0; i < montage.Segments.Count; i++)
                segmentSelection.Add(i);

            var notifySelection = new List<int>();
            for (int i = 0; i < montage.Notifies.Count; i++)
                notifySelection.Add(i);

            var stateSelection = new List<int>();
            for (int i = 0; i < montage.NotifyStates.Count; i++)
                stateSelection.Add(i);

            var customElementSelection = new List<int>();
            for (int i = 0; i < montage.CustomElements.Count; i++)
                customElementSelection.Add(i);

            context.SetSelectedTimelineElements(segmentSelection, notifySelection, stateSelection, customElementSelection, Array.Empty<string>());
            MarkDirtyRepaint();
        }

        private bool CopySelectionToClipboard()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return false;

            ClearClipboard();

            bool hasElementSelection = context.SelectedSegmentIndices.Count > 0
                || context.SelectedNotifyIndices.Count > 0
                || context.SelectedNotifyStateIndices.Count > 0
                || context.SelectedCustomElementIndices.Count > 0;

            if (hasElementSelection)
            {
                SerializedObject so = new(montage);
                CopySelectedSegments(so.FindProperty("segments"));
                CopySelectedNotifies(so.FindProperty("notifies"));
                CopySelectedNotifyStates(so.FindProperty("notifyStates"));
                CopySelectedCustomElements(so.FindProperty("customElements"));
                clipboardKind = copiedSegments.Count > 0 || copiedNotifies.Count > 0 || copiedNotifyStates.Count > 0 || copiedCustomElements.Count > 0
                    ? ClipboardContentKind.Elements
                    : ClipboardContentKind.None;
                return clipboardKind != ClipboardContentKind.None;
            }

            foreach (string key in context.SelectedTimelineTrackKeys)
            {
                if (TryParseTrackKey(key, out TrackIdentity identity))
                    copiedTracks.Add(identity);
            }

            clipboardKind = copiedTracks.Count > 0 ? ClipboardContentKind.Tracks : ClipboardContentKind.None;
            return clipboardKind != ClipboardContentKind.None;
        }

        private bool DuplicateSelection()
        {
            if (!CopySelectionToClipboard())
                return false;

            return PasteClipboard(false);
        }

        private bool PasteClipboard(bool alignToPointer)
        {
            return clipboardKind switch
            {
                ClipboardContentKind.Tracks => PasteCopiedTracks(),
                ClipboardContentKind.Elements => PasteCopiedElements(alignToPointer),
                _ => false
            };
        }

        private void ClearClipboard()
        {
            copiedTracks.Clear();
            copiedSegments.Clear();
            copiedNotifies.Clear();
            copiedNotifyStates.Clear();
            copiedCustomElements.Clear();
            clipboardKind = ClipboardContentKind.None;
        }

        private static T CloneManagedReference<T>(T source) where T : class
        {
            if (source == null)
                return null;

            Type type = source.GetType();
            if (Activator.CreateInstance(type) is not T clone)
                return null;

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            return clone;
        }

        private void CopySelectedSegments(SerializedProperty segments)
        {
            if (segments == null)
                return;

            foreach (int index in context.SelectedSegmentIndices)
            {
                if (index < 0 || index >= segments.arraySize)
                    continue;

                SerializedProperty segment = segments.GetArrayElementAtIndex(index);
                copiedSegments.Add(new SegmentClipboardData(
                    segment.FindPropertyRelative("sectionName")?.stringValue ?? "Default",
                    SanitizeTrackId(segment.FindPropertyRelative("trackId")?.stringValue),
                    segment.FindPropertyRelative("clip")?.objectReferenceValue as AnimationClip,
                    segment.FindPropertyRelative("startTime")?.floatValue ?? 0f,
                    segment.FindPropertyRelative("clipStartTime")?.floatValue ?? 0f,
                    segment.FindPropertyRelative("clipEndTime")?.floatValue ?? 0f,
                    segment.FindPropertyRelative("playRate")?.floatValue ?? 1f,
                    segment.FindPropertyRelative("blendIn")?.floatValue ?? 0f,
                    segment.FindPropertyRelative("blendOut")?.floatValue ?? 0f,
                    segment.FindPropertyRelative("customColor")?.colorValue ?? Color.clear));
            }
        }

        private void CopySelectedNotifies(SerializedProperty notifies)
        {
            if (notifies == null)
                return;

            foreach (int index in context.SelectedNotifyIndices)
            {
                if (index < 0 || index >= notifies.arraySize)
                    continue;

                SerializedProperty notify = notifies.GetArrayElementAtIndex(index);
                copiedNotifies.Add(new NotifyClipboardData(
                    notify.FindPropertyRelative("time")?.floatValue ?? 0f,
                    notify.FindPropertyRelative("notify")?.managedReferenceValue as AnimNotify,
                    SanitizeTrackId(notify.FindPropertyRelative("trackId")?.stringValue),
                    notify.FindPropertyRelative("triggerWeightThreshold")?.floatValue ?? 0f,
                    notify.FindPropertyRelative("customColor")?.colorValue ?? Color.clear));
            }
        }

        private void CopySelectedNotifyStates(SerializedProperty notifyStates)
        {
            if (notifyStates == null)
                return;

            foreach (int index in context.SelectedNotifyStateIndices)
            {
                if (index < 0 || index >= notifyStates.arraySize)
                    continue;

                SerializedProperty state = notifyStates.GetArrayElementAtIndex(index);
                copiedNotifyStates.Add(new NotifyStateClipboardData(
                    state.FindPropertyRelative("startTime")?.floatValue ?? 0f,
                    state.FindPropertyRelative("endTime")?.floatValue ?? 0f,
                    state.FindPropertyRelative("notifyState")?.managedReferenceValue as AnimNotifyState,
                    SanitizeTrackId(state.FindPropertyRelative("trackId")?.stringValue),
                    state.FindPropertyRelative("customColor")?.colorValue ?? Color.clear));
            }
        }

        private void CopySelectedCustomElements(SerializedProperty customElements)
        {
            if (customElements == null)
                return;

            foreach (int index in context.SelectedCustomElementIndices)
            {
                if (index < 0 || index >= customElements.arraySize)
                    continue;

                SerializedProperty element = customElements.GetArrayElementAtIndex(index);
                copiedCustomElements.Add(new CustomElementClipboardData(
                    element.FindPropertyRelative("startTime")?.floatValue ?? 0f,
                    element.FindPropertyRelative("endTime")?.floatValue ?? 0f,
                    element.FindPropertyRelative("element")?.managedReferenceValue as MontageTimelineElement,
                    SanitizeTrackId(element.FindPropertyRelative("trackId")?.stringValue),
                    element.FindPropertyRelative("customColor")?.colorValue ?? Color.clear));
            }
        }

        private bool PasteCopiedTracks()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || copiedTracks.Count == 0)
                return false;

            Undo.RecordObject(montage, "Duplicate Montage Track");
            SerializedObject so = new(montage);
            SerializedProperty order = so.FindProperty("timelineTrackOrder");
            var newTrackKeys = new List<string>();

            for (int i = 0; i < copiedTracks.Count; i++)
            {
                TrackIdentity source = copiedTracks[i];
                SerializedProperty tracks = so.FindProperty(GetTrackPropertyName(source.Kind));
                if (tracks == null)
                    continue;

                int index = tracks.arraySize;
                tracks.InsertArrayElementAtIndex(index);
                string trackId;
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                if (source.Kind == TrackKind.Custom)
                {
                    CustomMontageTrack sourceTrack = FindCustomTrack(montage, source.TrackId);
                    trackId = CreateUniqueCustomTrackId(tracks, $"{source.TrackId} Copy");
                    track.FindPropertyRelative("trackId").stringValue = trackId;
                    track.FindPropertyRelative("trackType").managedReferenceValue = CloneManagedReference(sourceTrack?.TrackType);
                    track.FindPropertyRelative("customColor").colorValue = sourceTrack?.CustomColor ?? Color.clear;
                }
                else
                {
                    trackId = CreateUniqueTrackId(tracks, $"{source.TrackId} Copy");
                    track.stringValue = trackId;
                }

                string key = GetTrackKey(source.Kind, trackId);
                if (order != null)
                    InsertTrackKeyAfter(order, GetTrackKey(source.Kind, source.TrackId), key);

                newTrackKeys.Add(key);
            }

            if (newTrackKeys.Count == 0)
                return false;

            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedTimelineTracks(newTrackKeys);
            MarkDirtyRepaint();
            return true;
        }

        private bool PasteCopiedElements(bool alignToPointer)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || !HasCopiedTimelineElements())
                return false;

            TrackIdentity? pasteTarget = GetElementPasteTarget();
            float timeOffset = alignToPointer ? Snap(XToTime(lastPointerLocal.x) - GetCopiedElementsMinTime()) : 0f;
            Undo.RecordObject(montage, "Paste Montage Timeline Elements");
            SerializedObject so = new(montage);
            var newSegments = new List<int>();
            var newNotifies = new List<int>();
            var newStates = new List<int>();
            var newCustomElements = new List<int>();

            PasteSegments(so.FindProperty("segments"), pasteTarget, timeOffset, newSegments);
            PasteNotifies(so.FindProperty("notifies"), pasteTarget, timeOffset, newNotifies);
            PasteNotifyStates(so.FindProperty("notifyStates"), pasteTarget, timeOffset, newStates);
            PasteCustomElements(so.FindProperty("customElements"), pasteTarget, timeOffset, newCustomElements);

            if (newSegments.Count == 0 && newNotifies.Count == 0 && newStates.Count == 0 && newCustomElements.Count == 0)
                return false;

            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedTimelineElements(newSegments, newNotifies, newStates, newCustomElements, Array.Empty<string>());
            MarkDirtyRepaint();
            return true;
        }

        private bool HasCopiedTimelineElements() =>
            copiedSegments.Count > 0
            || copiedNotifies.Count > 0
            || copiedNotifyStates.Count > 0
            || copiedCustomElements.Count > 0;

        private float GetCopiedElementsMinTime()
        {
            float minTime = float.PositiveInfinity;

            for (int i = 0; i < copiedSegments.Count; i++)
                minTime = Mathf.Min(minTime, copiedSegments[i].StartTime);

            for (int i = 0; i < copiedNotifies.Count; i++)
                minTime = Mathf.Min(minTime, copiedNotifies[i].Time);

            for (int i = 0; i < copiedNotifyStates.Count; i++)
                minTime = Mathf.Min(minTime, copiedNotifyStates[i].StartTime);

            for (int i = 0; i < copiedCustomElements.Count; i++)
                minTime = Mathf.Min(minTime, copiedCustomElements[i].StartTime);

            return float.IsPositiveInfinity(minTime) ? 0f : minTime;
        }

        private TrackIdentity? GetElementPasteTarget()
        {
            int typeCount = 0;
            TrackKind onlyKind = TrackKind.Segment;
            if (copiedSegments.Count > 0)
            {
                typeCount++;
                onlyKind = TrackKind.Segment;
            }

            if (copiedNotifies.Count > 0)
            {
                typeCount++;
                onlyKind = TrackKind.Notify;
            }

            if (copiedNotifyStates.Count > 0)
            {
                typeCount++;
                onlyKind = TrackKind.NotifyState;
            }

            if (copiedCustomElements.Count > 0)
            {
                typeCount++;
                onlyKind = TrackKind.Custom;
            }

            if (typeCount != 1 || !TryGetTrackRow(lastPointerLocal, onlyKind, out TrackRowLayout row))
                return null;

            return new TrackIdentity(row.Kind, row.TrackId);
        }

        private void PasteSegments(SerializedProperty segments, TrackIdentity? pasteTarget, float timeOffset, List<int> newIndices)
        {
            if (segments == null)
                return;

            for (int i = 0; i < copiedSegments.Count; i++)
            {
                SegmentClipboardData source = copiedSegments[i];
                int index = segments.arraySize;
                segments.InsertArrayElementAtIndex(index);
                SerializedProperty segment = segments.GetArrayElementAtIndex(index);
                segment.FindPropertyRelative("sectionName").stringValue = source.SectionName;
                segment.FindPropertyRelative("trackId").stringValue = pasteTarget.HasValue && pasteTarget.Value.Kind == TrackKind.Segment
                    ? pasteTarget.Value.TrackId
                    : source.TrackId;
                segment.FindPropertyRelative("clip").objectReferenceValue = source.Clip;
                segment.FindPropertyRelative("startTime").floatValue = Snap(Mathf.Max(0f, source.StartTime + timeOffset));
                segment.FindPropertyRelative("clipStartTime").floatValue = source.ClipStartTime;
                segment.FindPropertyRelative("clipEndTime").floatValue = source.ClipEndTime;
                segment.FindPropertyRelative("playRate").floatValue = source.PlayRate;
                segment.FindPropertyRelative("blendIn").floatValue = source.BlendIn;
                segment.FindPropertyRelative("blendOut").floatValue = source.BlendOut;
                segment.FindPropertyRelative("customColor").colorValue = source.CustomColor;
                newIndices.Add(index);
            }
        }

        private void PasteNotifies(SerializedProperty notifies, TrackIdentity? pasteTarget, float timeOffset, List<int> newIndices)
        {
            if (notifies == null)
                return;

            for (int i = 0; i < copiedNotifies.Count; i++)
            {
                NotifyClipboardData source = copiedNotifies[i];
                int index = notifies.arraySize;
                notifies.InsertArrayElementAtIndex(index);
                SerializedProperty notify = notifies.GetArrayElementAtIndex(index);
                notify.FindPropertyRelative("time").floatValue = Snap(Mathf.Max(0f, source.Time + timeOffset));
                notify.FindPropertyRelative("notify").managedReferenceValue = CloneManagedReference(source.Notify);
                notify.FindPropertyRelative("trackId").stringValue = pasteTarget.HasValue && pasteTarget.Value.Kind == TrackKind.Notify
                    ? pasteTarget.Value.TrackId
                    : source.TrackId;
                SerializedProperty trigger = notify.FindPropertyRelative("triggerWeightThreshold");
                if (trigger != null)
                    trigger.floatValue = source.TriggerWeightThreshold;
                notify.FindPropertyRelative("customColor").colorValue = source.CustomColor;
                newIndices.Add(index);
            }
        }

        private void PasteNotifyStates(SerializedProperty notifyStates, TrackIdentity? pasteTarget, float timeOffset, List<int> newIndices)
        {
            if (notifyStates == null)
                return;

            for (int i = 0; i < copiedNotifyStates.Count; i++)
            {
                NotifyStateClipboardData source = copiedNotifyStates[i];
                int index = notifyStates.arraySize;
                notifyStates.InsertArrayElementAtIndex(index);
                SerializedProperty state = notifyStates.GetArrayElementAtIndex(index);
                float startTime = Snap(Mathf.Max(0f, source.StartTime + timeOffset));
                state.FindPropertyRelative("startTime").floatValue = startTime;
                state.FindPropertyRelative("endTime").floatValue = Snap(Mathf.Max(startTime, source.EndTime + timeOffset));
                state.FindPropertyRelative("notifyState").managedReferenceValue = CloneManagedReference(source.NotifyState);
                state.FindPropertyRelative("trackId").stringValue = pasteTarget.HasValue && pasteTarget.Value.Kind == TrackKind.NotifyState
                    ? pasteTarget.Value.TrackId
                    : source.TrackId;
                state.FindPropertyRelative("customColor").colorValue = source.CustomColor;
                newIndices.Add(index);
            }
        }

        private void PasteCustomElements(SerializedProperty customElements, TrackIdentity? pasteTarget, float timeOffset, List<int> newIndices)
        {
            if (customElements == null)
                return;

            for (int i = 0; i < copiedCustomElements.Count; i++)
            {
                CustomElementClipboardData source = copiedCustomElements[i];
                int index = customElements.arraySize;
                customElements.InsertArrayElementAtIndex(index);
                SerializedProperty element = customElements.GetArrayElementAtIndex(index);
                float startTime = Snap(Mathf.Max(0f, source.StartTime + timeOffset));
                element.FindPropertyRelative("startTime").floatValue = startTime;
                element.FindPropertyRelative("endTime").floatValue = Snap(Mathf.Max(startTime, source.EndTime + timeOffset));
                element.FindPropertyRelative("element").managedReferenceValue = CloneManagedReference(source.Element);
                element.FindPropertyRelative("trackId").stringValue = pasteTarget.HasValue && pasteTarget.Value.Kind == TrackKind.Custom
                    ? pasteTarget.Value.TrackId
                    : source.TrackId;
                element.FindPropertyRelative("customColor").colorValue = source.CustomColor;
                newIndices.Add(index);
            }
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
                TrackKind.Custom => "customElements",
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

        private static int CountTrackItems(AnimMontageSO montage, TrackKind kind, string trackId)
        {
            if (montage == null)
                return 0;

            int count = 0;
            trackId = SanitizeTrackId(trackId);
            switch (kind)
            {
                case TrackKind.Segment:
                    for (int i = 0; i < montage.Segments.Count; i++)
                    {
                        MontageSegment segment = montage.Segments[i];
                        if (segment != null && segment.TrackId == trackId)
                            count++;
                    }

                    break;

                case TrackKind.Notify:
                    for (int i = 0; i < montage.Notifies.Count; i++)
                    {
                        AnimNotifyPlacement notify = montage.Notifies[i];
                        if (notify != null && notify.TrackId == trackId)
                            count++;
                    }

                    break;

                case TrackKind.NotifyState:
                    for (int i = 0; i < montage.NotifyStates.Count; i++)
                    {
                        AnimNotifyStatePlacement state = montage.NotifyStates[i];
                        if (state != null && state.TrackId == trackId)
                            count++;
                    }

                    break;

                case TrackKind.Custom:
                    for (int i = 0; i < montage.CustomElements.Count; i++)
                    {
                        CustomMontageElementPlacement element = montage.CustomElements[i];
                        if (element != null && element.TrackId == trackId)
                            count++;
                    }

                    break;
            }

            return count;
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

        private static void InsertTrackKeyAfter(SerializedProperty order, string sourceKey, string key)
        {
            int existingIndex = FindTrackKeyIndex(order, key);
            if (existingIndex >= 0)
                order.DeleteArrayElementAtIndex(existingIndex);

            int sourceIndex = FindTrackKeyIndex(order, sourceKey);
            int insertIndex = sourceIndex >= 0 ? sourceIndex + 1 : order.arraySize;
            order.InsertArrayElementAtIndex(Mathf.Clamp(insertIndex, 0, order.arraySize));
            order.GetArrayElementAtIndex(Mathf.Clamp(insertIndex, 0, order.arraySize - 1)).stringValue = key;
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

        private static string CreateUniqueCustomTrackId(SerializedProperty tracks, string displayName)
        {
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Custom" : displayName.Trim();
            int suffix = Mathf.Max(1, tracks.arraySize);
            while (true)
            {
                string candidate = $"{displayName} {suffix}";
                bool exists = false;
                for (int i = 0; i < tracks.arraySize; i++)
                {
                    SerializedProperty track = tracks.GetArrayElementAtIndex(i);
                    SerializedProperty trackId = track.FindPropertyRelative("trackId");
                    if (trackId != null && SanitizeTrackId(trackId.stringValue) == candidate)
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
                TrackKind.Custom => "customTracks",
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
            customElementLayouts.Clear();
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

                    case TrackKind.Custom:
                        y = DrawCustomTrack(painter, rect, y, montage, track.TrackId);
                        break;
                }
            }

            DrawSnapGuide(painter, rect);
            DrawPlayhead(painter, rect);
            DrawBoxSelection(painter);
            DrawPlayModeLockedOverlay(painter, rect);
        }

        private static void DrawPlayModeLockedOverlay(Painter2D painter, Rect rect)
        {
            if (!EditorApplication.isPlaying)
                return;

            painter.fillColor = new Color(0f, 0f, 0f, 0.28f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
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
                Color segmentColor = segment.HasCustomColor ? segment.CustomColor : SegmentCoreColor;

                painter.fillColor = selected ? HighlightColor(segmentColor) : segmentColor;
                FillRoundedRect(painter, clippedBody, 3f);
                painter.strokeColor = selected ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.22f);
                painter.lineWidth = selected ? 1.6f : 1f;
                StrokeRoundedRect(painter, clippedBody, 3f);

                DrawSegmentAutoBlendOverlay(painter, clippedBody, montage, segment, segmentIndex, contentRect);
                DrawSegmentLoopBadge(painter, clippedBody, segment.IsLoopingClip);

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

        private static void DrawSegmentLoopBadge(Painter2D painter, Rect segmentRect, bool isLooping)
        {
            if (segmentRect.width < 42f)
                return;

            float width = isLooping ? 20f : 18f;
            Rect badge = new(
                Mathf.Max(segmentRect.xMin + 4f, segmentRect.xMax - width - 4f),
                segmentRect.yMin + 4f,
                width,
                14f);

            painter.fillColor = isLooping
                ? new Color(0.08f, 0.46f, 0.24f, 0.9f)
                : new Color(0.08f, 0.08f, 0.09f, 0.72f);
            FillRoundedRect(painter, badge, 3f);
            painter.strokeColor = isLooping
                ? new Color(0.82f, 1f, 0.86f, 1f)
                : new Color(1f, 1f, 1f, 0.78f);
            painter.lineWidth = 1.2f;

            if (isLooping)
            {
                float cy = badge.center.y;
                painter.BeginPath();
                painter.Arc(new Vector2(badge.center.x - 1f, cy), 4f, 35f, 315f);
                painter.Stroke();
                DrawTriangle(painter, badge.center.x + 5f, cy - 2.5f, 3f, painter.strokeColor);
                return;
            }

            painter.BeginPath();
            painter.MoveTo(new Vector2(badge.xMin + 5f, badge.center.y));
            painter.LineTo(new Vector2(badge.xMax - 5f, badge.center.y));
            painter.Stroke();
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

                Color color = placement.HasCustomColor
                    ? placement.CustomColor
                    : placement.Notify != null ? placement.Notify.EditorColor : new Color(0.4f, 0.8f, 1f);
                float x = TimeToX(placement.Time);
                AudioClip audioClip = GetAudioClipFromNotify(placement.Notify);
                float visibleEndX = audioClip != null ? TimeToX(placement.Time + audioClip.length) : x;
                if (visibleEndX < contentRect.xMin - 8f || x > contentRect.xMax + 8f)
                    continue;

                DrawNotifyAudioWaveform(painter, placement, audioClip, y, contentRect, color);
                if (x >= contentRect.xMin - 8f && x <= contentRect.xMax + 8f)
                    DrawDiamond(painter, x, y + TrackHeight * 0.5f, 7f, color);
                var hitRect = new Rect(x - 9f, y + TrackHeight * 0.5f - 9f, 18f, 18f);
                notifyLayouts.Add(new NotifyLayout(i, hitRect));
                if (context.IsNotifySelected(i) && x >= contentRect.xMin - 8f && x <= contentRect.xMax + 8f)
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.78f);
                    painter.lineWidth = 1.5f;
                    StrokeDiamond(painter, x, y + TrackHeight * 0.5f, 9.5f);
                }
            }

            return y + TrackHeight + TrackGap;
        }

        private void DrawNotifyAudioWaveform(
            Painter2D painter,
            AnimNotifyPlacement placement,
            AudioClip clip,
            float y,
            Rect contentRect,
            Color color)
        {
            if (clip == null || clip.length <= 0f)
                return;

            float x0 = TimeToX(placement.Time);
            float x1 = TimeToX(placement.Time + clip.length);
            var body = new Rect(x0, y + 5f, Mathf.Max(8f, x1 - x0), TrackHeight - 10f);
            if (!TryClipRect(body, contentRect, out Rect clippedBody))
                return;

            painter.fillColor = new Color(color.r, color.g, color.b, 0.16f);
            FillRoundedRect(painter, clippedBody, 3f);
            painter.strokeColor = new Color(color.r, color.g, color.b, 0.5f);
            painter.lineWidth = 1f;
            StrokeRoundedRect(painter, clippedBody, 3f);

            float[] peaks = GetAudioWaveformPeaks(clip);
            if (peaks == null || peaks.Length == 0 || clippedBody.width < 6f)
                return;

            painter.strokeColor = new Color(0.72f, 0.9f, 1f, 0.82f);
            painter.lineWidth = 1f;
            float centerY = clippedBody.center.y;
            float amplitude = clippedBody.height * 0.42f;
            int columns = Mathf.Clamp(Mathf.FloorToInt(clippedBody.width), 4, peaks.Length);
            for (int i = 0; i < columns; i++)
            {
                int peakIndex = Mathf.Clamp(Mathf.RoundToInt(i / Mathf.Max(1f, columns - 1f) * (peaks.Length - 1)), 0, peaks.Length - 1);
                float x = Mathf.Lerp(clippedBody.xMin + 2f, clippedBody.xMax - 2f, i / Mathf.Max(1f, columns - 1f));
                float fallback = 0.16f + 0.1f * Mathf.Sin(i * 0.55f);
                float h = Mathf.Max(1f, Mathf.Max(peaks[peakIndex], fallback) * amplitude);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, centerY - h));
                painter.LineTo(new Vector2(x, centerY + h));
                painter.Stroke();
            }
        }

        private static AudioClip GetAudioClipFromNotify(AnimNotify notify)
        {
            if (notify == null)
                return null;

            FieldInfo field = notify.GetType().GetField("clip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(AudioClip)
                ? field.GetValue(notify) as AudioClip
                : null;
        }

        private float[] GetAudioWaveformPeaks(AudioClip clip)
        {
            if (audioWaveformCache.TryGetValue(clip, out float[] peaks))
                return peaks;

            const int PeakCount = 96;
            peaks = new float[PeakCount];
            audioWaveformCache[clip] = peaks;
            if (clip == null || clip.samples <= 0 || clip.channels <= 0)
                return peaks;

            int sampleCount = Mathf.Min(clip.samples * clip.channels, 44100 * clip.channels * 20);
            float[] samples = new float[sampleCount];
            try
            {
                if (!clip.GetData(samples, 0))
                    return peaks;
            }
            catch
            {
                return peaks;
            }

            int samplesPerPeak = Mathf.Max(1, sampleCount / PeakCount);
            for (int i = 0; i < PeakCount; i++)
            {
                int start = i * samplesPerPeak;
                int end = Mathf.Min(sampleCount, start + samplesPerPeak);
                float max = 0f;
                for (int j = start; j < end; j++)
                    max = Mathf.Max(max, Mathf.Abs(samples[j]));

                peaks[i] = Mathf.Clamp01(max);
            }

            return peaks;
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
                Color stateColor = placement.HasCustomColor
                    ? placement.CustomColor
                    : placement.NotifyState.EditorColor;
                Color bodyColor = selected
                    ? HighlightColor(stateColor) * new Color(1f, 1f, 1f, 0.9f)
                    : stateColor * new Color(1f, 1f, 1f, 0.62f);
                Rect body = new(
                    clippedBar.xMin,
                    clippedBar.yMin + 3f,
                    clippedBar.width,
                    Mathf.Max(1f, clippedBar.height - 6f));
                painter.fillColor = bodyColor;
                FillRoundedRect(painter, body, 4f);

                painter.strokeColor = selected
                    ? new Color(1f, 1f, 1f, 0.9f)
                    : new Color(1f, 1f, 1f, 0.36f);
                painter.lineWidth = selected ? 1.6f : 1f;
                StrokeRoundedRect(painter, body, 4f);

                Rect inner = new(
                    body.xMin + 2f,
                    body.yMin + 2f,
                    Mathf.Max(0f, body.width - 4f),
                    Mathf.Max(0f, body.height * 0.42f));
                if (inner.width > 0f && inner.height > 0f)
                {
                    painter.fillColor = new Color(1f, 1f, 1f, selected ? 0.22f : 0.14f);
                    FillRoundedRect(painter, inner, 3f);
                }

                DrawNotifyStateAudioWaveform(painter, placement, body, selected);
                DrawSegmentEdgeTicks(painter, body);

                float gripWidth = Mathf.Min(5f, Mathf.Max(2f, body.width * 0.25f));
                if (body.width >= 8f)
                {
                    painter.fillColor = new Color(0f, 0f, 0f, selected ? 0.34f : 0.22f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(body.xMin + gripWidth, body.yMin));
                    painter.LineTo(new Vector2(body.xMin, body.center.y));
                    painter.LineTo(new Vector2(body.xMin + gripWidth, body.yMax));
                    painter.ClosePath();
                    painter.Fill();

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(body.xMax - gripWidth, body.yMin));
                    painter.LineTo(new Vector2(body.xMax, body.center.y));
                    painter.LineTo(new Vector2(body.xMax - gripWidth, body.yMax));
                    painter.ClosePath();
                    painter.Fill();
                }

                painter.strokeColor = new Color(0f, 0f, 0f, selected ? 0.34f : 0.2f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(body.xMin + gripWidth, body.center.y));
                painter.LineTo(new Vector2(body.xMax - gripWidth, body.center.y));
                painter.Stroke();
                notifyStateLayouts.Add(new NotifyStateLayout(i, clippedBar));
            }

            return y + TrackHeight + TrackGap;
        }

        private void DrawNotifyStateAudioWaveform(
            Painter2D painter,
            AnimNotifyStatePlacement placement,
            Rect body,
            bool selected)
        {
            AudioClip clip = GetAudioClipFromNotifyState(placement.NotifyState);
            if (clip == null || clip.length <= 0f || body.width < 8f)
                return;

            float[] peaks = GetAudioWaveformPeaks(clip);
            if (peaks == null || peaks.Length == 0)
                return;

            Rect waveRect = new(
                body.xMin + 6f,
                body.yMin + 6f,
                Mathf.Max(0f, body.width - 12f),
                Mathf.Max(0f, body.height - 12f));
            if (waveRect.width <= 2f || waveRect.height <= 2f)
                return;

            painter.strokeColor = selected
                ? new Color(1f, 0.96f, 0.72f, 0.95f)
                : new Color(1f, 0.93f, 0.62f, 0.76f);
            painter.lineWidth = 1f;

            float centerY = waveRect.center.y;
            float amplitude = waveRect.height * 0.46f;
            int columns = Mathf.Clamp(Mathf.FloorToInt(waveRect.width), 4, 240);
            for (int i = 0; i < columns; i++)
            {
                float normalized = i / Mathf.Max(1f, columns - 1f);
                float timeInState = normalized * Mathf.Max(0.0001f, placement.Duration);
                float clipPhase = Mathf.Repeat(timeInState, clip.length) / clip.length;
                int peakIndex = Mathf.Clamp(Mathf.RoundToInt(clipPhase * (peaks.Length - 1)), 0, peaks.Length - 1);
                float x = Mathf.Lerp(waveRect.xMin, waveRect.xMax, normalized);
                float fallback = 0.16f + 0.1f * Mathf.Sin(i * 0.55f);
                float h = Mathf.Max(1f, Mathf.Max(peaks[peakIndex], fallback) * amplitude);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, centerY - h));
                painter.LineTo(new Vector2(x, centerY + h));
                painter.Stroke();
            }
        }

        private static AudioClip GetAudioClipFromNotifyState(AnimNotifyState notifyState)
        {
            if (notifyState == null)
                return null;

            FieldInfo field = notifyState.GetType().GetField("clip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(AudioClip)
                ? field.GetValue(notifyState) as AudioClip
                : null;
        }

        private float DrawCustomTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            CustomMontageTrack track = FindCustomTrack(montage, trackId);
            Color trackColor = track != null && track.HasCustomColor
                ? track.CustomColor
                : track?.TrackType != null ? track.TrackType.EditorColor : new Color(0.62f, 0.44f, 0.86f, 0.32f);
            DrawTrackRow(painter, rect, y, trackColor * new Color(1f, 1f, 1f, 0.35f), TrackKind.Custom, trackId);
            trackRows.Add(new TrackRowLayout(TrackKind.Custom, trackId, new Rect(rect.xMin, y, rect.width, TrackHeight)));
            Rect contentRect = GetTimelineContentRect(rect);

            for (int i = 0; i < montage.CustomElements.Count; i++)
            {
                CustomMontageElementPlacement placement = montage.CustomElements[i];
                if (placement == null || placement.TrackId != trackId)
                    continue;

                float x0 = TimeToX(placement.StartTime);
                float x1 = TimeToX(placement.EndTime);
                var body = new Rect(x0, y + 4f, Mathf.Max(4f, x1 - x0), TrackHeight - 8f);
                if (!TryClipRect(body, contentRect, out Rect clippedBody))
                    continue;

                Color elementColor = placement.HasCustomColor
                    ? placement.CustomColor
                    : placement.Element != null ? placement.Element.EditorColor : trackColor;
                bool selected = context.IsCustomElementSelected(i);
                painter.fillColor = selected
                    ? HighlightColor(elementColor)
                    : elementColor * new Color(1f, 1f, 1f, 0.72f);
                FillRoundedRect(painter, clippedBody, 3f);
                painter.strokeColor = selected ? new Color(1f, 1f, 1f, 0.72f) : new Color(1f, 1f, 1f, 0.28f);
                painter.lineWidth = selected ? 1.6f : 1f;
                StrokeRoundedRect(painter, clippedBody, 3f);
                DrawSegmentEdgeTicks(painter, clippedBody);
                customElementLayouts.Add(new CustomElementLayout(i, clippedBody));
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

        private static void DrawTriangle(Painter2D painter, float cx, float cy, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx + radius, cy));
            painter.LineTo(new Vector2(cx - radius, cy - radius));
            painter.LineTo(new Vector2(cx - radius, cy + radius));
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
            return local.y <= RulerHeight && distance <= 6f;
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

        private bool TryHitCustomElement(Vector2 local, out int index) =>
            TryHitCustomElement(local, out index, out _);

        private bool TryHitCustomElement(Vector2 local, out int index, out DragMode mode)
        {
            index = -1;
            mode = DragMode.None;
            for (int i = customElementLayouts.Count - 1; i >= 0; i--)
            {
                CustomElementLayout layout = customElementLayouts[i];
                if (!layout.Body.Contains(local))
                    continue;

                index = layout.Index;
                if (Mathf.Abs(local.x - layout.Body.xMin) <= EdgeHandleWidth)
                {
                    mode = DragMode.CustomElementResizeStart;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMax) <= EdgeHandleWidth)
                {
                    mode = DragMode.CustomElementResizeEnd;
                    return true;
                }

                mode = DragMode.CustomElementMove;
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
                    UpdateHoverTooltip(local, new HoverTooltipInfo(GetTrackDisplayName(row.Kind, row.TrackId)));
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
                if (!layout.Body.Contains(local) || !IsValidIndex(layout.Index, context.Montage.Segments))
                    continue;

                MontageSegment segment = context.Montage.Segments[layout.Index];
                string sectionName = segment == null || string.IsNullOrEmpty(segment.SectionName) ? "Animation Clip" : segment.SectionName;
                tooltip = new HoverTooltipInfo(sectionName, 120f);
                return true;
            }

            for (int i = notifyStateLayouts.Count - 1; i >= 0; i--)
            {
                NotifyStateLayout layout = notifyStateLayouts[i];
                if (!layout.Body.Contains(local) || !IsValidIndex(layout.Index, context.Montage.NotifyStates))
                    continue;

                AnimNotifyStatePlacement placement = context.Montage.NotifyStates[layout.Index];
                string stateName = placement.NotifyState != null ? placement.NotifyState.DisplayName : "Notify State";
                tooltip = new HoverTooltipInfo(stateName, 120f);
                return true;
            }

            if (TryHitNotify(local, out int notifyIndex))
            {
                if (!IsValidIndex(notifyIndex, context.Montage.Notifies))
                    return false;

                AnimNotifyPlacement placement = context.Montage.Notifies[notifyIndex];
                string notifyName = placement.Notify != null ? placement.Notify.DisplayName : "Notify";
                tooltip = new HoverTooltipInfo(notifyName, 96f);
                return true;
            }

            if (TryHitCustomElement(local, out int customElementIndex))
            {
                if (!IsValidIndex(customElementIndex, context.Montage.CustomElements))
                    return false;

                CustomMontageElementPlacement placement = context.Montage.CustomElements[customElementIndex];
                string elementName = placement.Element != null ? placement.Element.DisplayName : "Custom Element";
                tooltip = new HoverTooltipInfo(elementName, 120f);
                return true;
            }

            return false;
        }

        private static bool IsValidIndex<T>(int index, IReadOnlyList<T> items) =>
            items != null && index >= 0 && index < items.Count;
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

        private string GetTrackDisplayName(TrackKind kind, string trackId)
        {
            if (kind == TrackKind.Custom && context?.Montage != null)
            {
                CustomMontageTrack track = FindCustomTrack(context.Montage, trackId);
                if (track?.TrackType != null)
                    return track.TrackType.DisplayName;
            }

            return GetTrackTypeName(kind);
        }
        private static string GetTrackTypeName(TrackKind kind) =>
            kind switch
            {
                TrackKind.Segment => "Animation",
                TrackKind.Notify => "Notify",
                TrackKind.NotifyState => "Notify State",
                TrackKind.Custom => "Custom",
                _ => "Track"
            };

        private static Color HighlightColor(Color color) =>
            new(
                Mathf.Clamp01(color.r + 0.12f),
                Mathf.Clamp01(color.g + 0.12f),
                Mathf.Clamp01(color.b + 0.12f),
                Mathf.Clamp(color.a <= 0f ? 0.98f : color.a, 0.55f, 1f));

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
            AddTrackIdentities(tracks, TrackKind.Custom, GetCustomTrackIds(montage));
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

        private static List<string> GetCustomTrackIds(AnimMontageSO montage)
        {
            List<string> tracks = new();
            for (int i = 0; i < montage.CustomTracks.Count; i++)
            {
                CustomMontageTrack track = montage.CustomTracks[i];
                if (track != null)
                    AddTrackId(tracks, track.TrackId);
            }

            for (int i = 0; i < montage.CustomElements.Count; i++)
            {
                CustomMontageElementPlacement element = montage.CustomElements[i];
                if (element != null)
                    AddTrackId(tracks, element.TrackId);
            }

            return tracks;
        }

        private static CustomMontageTrack FindCustomTrack(AnimMontageSO montage, string trackId)
        {
            trackId = SanitizeTrackId(trackId);
            for (int i = 0; i < montage.CustomTracks.Count; i++)
            {
                CustomMontageTrack track = montage.CustomTracks[i];
                if (track != null && track.TrackId == trackId)
                    return track;
            }

            return null;
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