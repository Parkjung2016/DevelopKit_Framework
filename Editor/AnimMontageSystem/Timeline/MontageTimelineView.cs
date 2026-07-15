using System;
using System.Collections.Generic;
using System.Reflection;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed partial class MontageTimelineView : VisualElement
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
        private static readonly Color EmptySegmentColor = new(0.32f, 0.38f, 0.46f, 0.94f);
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
            NotifyState,
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

        private sealed class SegmentIndexComparer : IComparer<int>
        {
            public IReadOnlyList<MontageSegment> Segments { private get; set; }

            public int Compare(int left, int right)
            {
                MontageSegment leftSegment = Segments[left];
                MontageSegment rightSegment = Segments[right];
                int timeCompare = leftSegment.StartTime.CompareTo(rightSegment.StartTime);
                return timeCompare != 0 ? timeCompare : left.CompareTo(right);
            }
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
                MontageSegmentType segmentType,
                AnimationClip clip,
                float startTime,
                float emptyStateDuration,
                float clipStartTime,
                float clipEndTime,
                float playRate,
                float blendIn,
                float blendOut,
                Color customColor)
            {
                SectionName = sectionName;
                TrackId = trackId;
                SegmentType = segmentType;
                Clip = clip;
                StartTime = startTime;
                EmptyStateDuration = emptyStateDuration;
                ClipStartTime = clipStartTime;
                ClipEndTime = clipEndTime;
                PlayRate = playRate;
                BlendIn = blendIn;
                BlendOut = blendOut;
                CustomColor = customColor;
            }

            public string SectionName { get; }
            public string TrackId { get; }
            public MontageSegmentType SegmentType { get; }
            public AnimationClip Clip { get; }
            public float StartTime { get; }
            public float EmptyStateDuration { get; }
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

        private readonly MontageEditorContext context;
        private readonly List<SegmentLayout> segmentLayouts = new();
        private readonly List<NotifyLayout> notifyLayouts = new();
        private readonly List<NotifyStateLayout> notifyStateLayouts = new();
        private readonly List<TrackRowLayout> trackRows = new();
        private readonly List<TrackIdentity> orderedTrackBuffer = new();
        private readonly List<TrackIdentity> allTrackBuffer = new();
        private readonly List<int> segmentIndexBuffer = new();
        private readonly SegmentIndexComparer segmentIndexComparer = new();
        private readonly Dictionary<int, float> dragSegmentStartTimes = new();
        private readonly Dictionary<int, float> dragNotifyTimes = new();
        private readonly Dictionary<int, Vector2> dragNotifyStateRanges = new();
        private readonly List<TrackIdentity> copiedTracks = new();
        private readonly List<SegmentClipboardData> copiedSegments = new();
        private readonly List<NotifyClipboardData> copiedNotifies = new();
        private readonly List<NotifyStateClipboardData> copiedNotifyStates = new();
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
                if (CanReplaceSegmentClip(segmentIndex))
                {
                    menu.AddItem(new GUIContent("Segment/Replace Clip..."), false, () => OpenCreatePicker(PendingCreateKind.ReplaceSegmentClip, time, "Default", segmentIndex));
                    if (Selection.activeObject is AnimationClip replacementClip && IsCompatibleAnimationClip(replacementClip))
                        menu.AddItem(new GUIContent("Segment/Replace Clip From Project Selection"), false, () => ReplaceSegmentClip(segmentIndex, replacementClip));
                    else
                        menu.AddDisabledItem(new GUIContent("Segment/Replace Clip From Project Selection"));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Segment/Replace Clip..."));
                    menu.AddDisabledItem(new GUIContent("Segment/Replace Clip From Project Selection"));
                }
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

            if (hasRow && row.Kind == TrackKind.Segment)
            {
                string trackId = row.TrackId;
                menu.AddItem(new GUIContent("Create/Animation Segment..."), false, () => OpenCreatePicker(PendingCreateKind.Segment, time, trackId));
                menu.AddItem(new GUIContent("Create/Empty State"), false, () => AddEmptyStateAtTime(time, trackId));
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
                        IsCompatibleAnimationClip,
                        "Empty State",
                        () => AddEmptyStateAtTime(time, trackId));
                    break;

                case PendingCreateKind.ReplaceSegmentClip:
                    if (!CanReplaceSegmentClip(editIndex))
                        break;

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

            }
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

        private static string GetTrackDisplayName(TrackKind kind, string trackId) =>
            GetTrackTypeName(kind);
        private static string GetTrackTypeName(TrackKind kind) =>
            kind switch
            {
                TrackKind.Segment => "Animation",
                TrackKind.Notify => "Notify",
                TrackKind.NotifyState => "Notify State",
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
            var ordered = new List<TrackIdentity>();
            var all = new List<TrackIdentity>();
            FillOrderedTimelineTracks(montage, ordered, all);
            return ordered;
        }

        private static void FillOrderedTimelineTracks(
            AnimMontageSO montage,
            List<TrackIdentity> ordered,
            List<TrackIdentity> all)
        {
            ordered.Clear();
            FillAllTimelineTracks(montage, all);

            IReadOnlyList<string> order = montage.TimelineTrackOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (!TryParseTrackKey(order[i], out TrackIdentity identity)
                    || !ContainsTrack(all, identity.Kind, identity.TrackId)
                    || ContainsTrack(ordered, identity.Kind, identity.TrackId))
                    continue;

                ordered.Add(identity);
            }

            for (int i = 0; i < all.Count; i++)
            {
                TrackIdentity identity = all[i];
                if (!ContainsTrack(ordered, identity.Kind, identity.TrackId))
                    ordered.Add(identity);
            }
        }

        private static void FillAllTimelineTracks(AnimMontageSO montage, List<TrackIdentity> tracks)
        {
            tracks.Clear();
            AddConfiguredTracks(tracks, TrackKind.Segment, montage.AnimationTracks);
            for (int i = 0; i < montage.Segments.Count; i++)
            {
                MontageSegment segment = montage.Segments[i];
                if (segment != null)
                    AddTrackIdentity(tracks, TrackKind.Segment, segment.TrackId);
            }

            AddConfiguredTracks(tracks, TrackKind.Notify, montage.NotifyTracks);
            for (int i = 0; i < montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement notify = montage.Notifies[i];
                if (notify != null)
                    AddTrackIdentity(tracks, TrackKind.Notify, notify.TrackId);
            }

            AddConfiguredTracks(tracks, TrackKind.NotifyState, montage.NotifyStateTracks);
            for (int i = 0; i < montage.NotifyStates.Count; i++)
            {
                AnimNotifyStatePlacement state = montage.NotifyStates[i];
                if (state != null)
                    AddTrackIdentity(tracks, TrackKind.NotifyState, state.TrackId);
            }
        }

        private static void AddConfiguredTracks(
            List<TrackIdentity> tracks,
            TrackKind kind,
            IReadOnlyList<string> trackIds)
        {
            if (trackIds == null || trackIds.Count == 0)
            {
                AddTrackIdentity(tracks, kind, "Default");
                return;
            }

            for (int i = 0; i < trackIds.Count; i++)
                AddTrackIdentity(tracks, kind, trackIds[i]);
        }

        private static void AddTrackIdentity(List<TrackIdentity> tracks, TrackKind kind, string trackId)
        {
            trackId = SanitizeTrackId(trackId);
            if (!ContainsTrack(tracks, kind, trackId))
                tracks.Add(new TrackIdentity(kind, trackId));
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

        private void FillSegmentIndicesForTrack(AnimMontageSO montage, string trackId)
        {
            segmentIndexBuffer.Clear();
            trackId = SanitizeTrackId(trackId);
            for (int i = 0; i < montage.Segments.Count; i++)
            {
                MontageSegment segment = montage.Segments[i];
                if (segment != null && segment.TrackId == trackId)
                    segmentIndexBuffer.Add(i);
            }

            segmentIndexComparer.Segments = montage.Segments;
            segmentIndexBuffer.Sort(segmentIndexComparer);
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