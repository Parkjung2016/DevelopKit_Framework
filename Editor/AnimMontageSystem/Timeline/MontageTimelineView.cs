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
        private const float BlendHandleHitWidth = 22f;
        private const float TransitionHandleWidth = 22f;
        private const float MagneticSnapPixels = 10f;
        private const float MinSegmentDuration = 0.05f;
        private const float DefaultQuickBlendDuration = 0.2f;

        private static readonly Color SegmentCoreColor = new(0.28f, 0.52f, 0.92f, 0.92f);
        private static readonly Color SegmentSelectedColor = new(0.38f, 0.64f, 1f, 0.98f);
        private static readonly Color BlendOverlayColor = new(0.08f, 0.12f, 0.22f, 0.55f);
        private static readonly Color BlendHandleColor = new(1f, 1f, 1f, 0.62f);
        private static readonly Color TransitionColor = new(0.62f, 0.38f, 0.95f, 0.42f);
        private static readonly Color TransitionHandleColor = new(0.8f, 0.56f, 1f, 0.72f);
        private static readonly Color TrackRowColor = new(0.1f, 0.1f, 0.11f, 1f);

        private enum DragMode
        {
            None,
            Playhead,
            SegmentMove,
            SegmentBlendIn,
            SegmentBlendOut,
            TransitionCrossfade,
            TimelinePan,
            NotifyMove,
            NotifyStateMove,
            NotifyStateResizeStart,
            NotifyStateResizeEnd
        }

        private enum PendingCreateKind
        {
            None,
            Segment,
            Notify,
            NotifyState
        }

        private readonly struct TransitionLayout
        {
            public TransitionLayout(int lowerSegmentIndex, Rect rect, float duration)
            {
                LowerSegmentIndex = lowerSegmentIndex;
                Rect = rect;
                Duration = duration;
            }

            public int LowerSegmentIndex { get; }
            public Rect Rect { get; }
            public float Duration { get; }
        }

        private readonly struct SegmentLayout
        {
            public SegmentLayout(int index, Rect body, Rect blendIn, Rect blendOut)
            {
                Index = index;
                Body = body;
                BlendIn = blendIn;
                BlendOut = blendOut;
            }

            public int Index { get; }
            public Rect Body { get; }
            public Rect BlendIn { get; }
            public Rect BlendOut { get; }
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

        private readonly MontageEditorContext context;
        private readonly List<SegmentLayout> segmentLayouts = new();
        private readonly List<TransitionLayout> transitionLayouts = new();
        private readonly List<NotifyStateLayout> notifyStateLayouts = new();

        private float pixelsPerSecond = 120f;
        private float viewStartTime;
        private float segmentTrackTop;
        private float notifyTrackTop;
        private float notifyStateTrackTop;

        private DragMode dragMode = DragMode.None;
        private int dragSegmentIndex = -1;
        private int dragNotifyIndex = -1;
        private int dragNotifyStateIndex = -1;
        private float dragAnchorTime;
        private float dragAnchorValue;
        private float dragNotifyStateDuration;
        private bool hasSnapGuide;
        private float snapGuideTime;
        private PendingCreateKind pendingCreateKind = PendingCreateKind.None;
        private float pendingCreateTime;
        private int pendingObjectPickerControlId;
        private int objectPickerSerial;

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

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand, TrickleDown.TrickleDown);
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedoPerformed);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedoPerformed);

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

            if (!DeleteSelected())
                return;

            evt.StopPropagation();
        }

        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            if (evt.commandName != "ObjectSelectorClosed")
                return;

            if (pendingCreateKind == PendingCreateKind.None
                || EditorGUIUtility.GetObjectPickerControlID() != pendingObjectPickerControlId)
                return;

            UnityEngine.Object picked = EditorGUIUtility.GetObjectPickerObject();
            switch (pendingCreateKind)
            {
                case PendingCreateKind.Segment when picked is AnimationClip clip:
                    AddSegmentAtTime(pendingCreateTime, clip);
                    break;

                case PendingCreateKind.Notify when picked is AnimNotifySO notify:
                    AddNotifyAtTime(pendingCreateTime, notify);
                    break;

                case PendingCreateKind.NotifyState when picked is AnimNotifyStateSO notifyState:
                    AddNotifyStateAtTime(pendingCreateTime, notifyState);
                    break;
            }

            pendingCreateKind = PendingCreateKind.None;
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
                evt.StopPropagation();
                return;
            }

            if (evt.button == 1)
            {
                ShowContextMenu(local);
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
                BeginDrag(segmentDrag, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                dragAnchorValue = GetSegmentDragValue(segmentDrag, dragSegmentIndex);
                context.SetSelectedSegment(dragSegmentIndex);
                evt.StopPropagation();
                return;
            }

            if (TryHitTransition(local, out dragSegmentIndex, out float transitionDuration))
            {
                context.SetSelectedSegment(dragSegmentIndex);
                if (evt.clickCount == 2)
                {
                    ApplyTransitionCrossfade(
                        dragSegmentIndex,
                        transitionDuration > 0.0001f ? 0f : DefaultQuickBlendDuration);
                    evt.StopPropagation();
                    return;
                }

                BeginDrag(DragMode.TransitionCrossfade, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                dragAnchorValue = transitionDuration;
                evt.StopPropagation();
                return;
            }

            if (TryHitNotifyState(local, out dragNotifyStateIndex, out DragMode stateDrag))
            {
                BeginDrag(stateDrag, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                AnimNotifyStatePlacement placement = context.Montage.NotifyStates[dragNotifyStateIndex];
                dragNotifyStateDuration = placement.Duration;
                dragAnchorValue = stateDrag == DragMode.NotifyStateResizeEnd
                    ? placement.EndTime
                    : placement.StartTime;
                context.SetSelectedNotifyState(dragNotifyStateIndex);
                evt.StopPropagation();
                return;
            }

            if (TryHitNotify(local, out dragNotifyIndex))
            {
                BeginDrag(DragMode.NotifyMove, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                context.SetSelectedNotify(dragNotifyIndex);
                evt.StopPropagation();
                return;
            }

            if (evt.clickCount == 2 && local.y > RulerHeight)
            {
                AddNotifyAtTime(XToTime(local.x));
                evt.StopPropagation();
                return;
            }

            if (local.y <= RulerHeight)
            {
                BeginDrag(DragMode.Playhead, evt.pointerId);
                SetPlayheadFromX(local.x);
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;

            float time = XToTime(evt.localPosition.x);
            hasSnapGuide = false;
            switch (dragMode)
            {
                case DragMode.Playhead:
                    context.SetPlayhead(Snap(time));
                    break;

                case DragMode.NotifyMove when dragNotifyIndex >= 0:
                    ApplyNotifyTime(dragNotifyIndex, time);
                    break;

                case DragMode.SegmentMove when dragSegmentIndex >= 0:
                    ApplySegmentStartTime(dragSegmentIndex, time - dragAnchorTime + dragAnchorValue);
                    break;

                case DragMode.SegmentBlendIn when dragSegmentIndex >= 0:
                    ApplySegmentBlendIn(dragSegmentIndex, time);
                    break;

                case DragMode.SegmentBlendOut when dragSegmentIndex >= 0:
                    ApplySegmentBlendOut(dragSegmentIndex, time);
                    break;

                case DragMode.TransitionCrossfade when dragSegmentIndex >= 0:
                    ApplyTransitionCrossfade(
                        dragSegmentIndex,
                        dragAnchorValue <= 0.0001f
                            ? Mathf.Abs(time - dragAnchorTime)
                            : dragAnchorValue + (time - dragAnchorTime));
                    break;

                case DragMode.TimelinePan:
                    viewStartTime = dragAnchorValue - (evt.localPosition.x - dragAnchorTime) / Mathf.Max(1f, pixelsPerSecond);
                    ClampViewStartTime();
                    MarkDirtyRepaint();
                    break;

                case DragMode.NotifyStateMove when dragNotifyStateIndex >= 0:
                    ApplyNotifyStateRange(
                        dragNotifyStateIndex,
                        time - dragAnchorTime + dragAnchorValue,
                        time - dragAnchorTime + dragAnchorValue + dragNotifyStateDuration);
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

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;

            this.ReleasePointer(evt.pointerId);
            dragMode = DragMode.None;
            dragSegmentIndex = -1;
            dragNotifyIndex = -1;
            dragNotifyStateIndex = -1;
            hasSnapGuide = false;
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void BeginDrag(DragMode mode, int pointerId)
        {
            dragMode = mode;
            this.CapturePointer(pointerId);
        }

        private void ShowContextMenu(Vector2 local)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            float time = Snap(XToTime(local.x));
            GenericMenu menu = new();

            if (TryHitSegment(local, out int segmentIndex, out _))
            {
                menu.AddItem(new GUIContent("Segment/Delete"), false, () => DeleteArrayElement("segments", segmentIndex, "Delete Montage Segment"));
                menu.AddItem(new GUIContent("Segment/Select"), false, () => context.SetSelectedSegment(segmentIndex));
                menu.AddSeparator("");
            }

            if (TryHitNotify(local, out int notifyIndex))
            {
                menu.AddItem(new GUIContent("Notify/Delete"), false, () => DeleteArrayElement("notifies", notifyIndex, "Delete Anim Notify"));
                menu.AddSeparator("");
            }

            if (TryHitNotifyState(local, out int notifyStateIndex, out _))
            {
                menu.AddItem(new GUIContent("Notify State/Delete"), false, () => DeleteArrayElement("notifyStates", notifyStateIndex, "Delete Anim Notify State"));
                menu.AddSeparator("");
            }

            if (IsSegmentTrack(local))
            {
                menu.AddItem(new GUIContent("Create/Animation Segment..."), false, () => OpenCreatePicker(PendingCreateKind.Segment, time));
                if (Selection.activeObject is AnimationClip selectedClip)
                    menu.AddItem(new GUIContent("Create/Segment From Project Selection"), false, () => AddSegmentAtTime(time, selectedClip));
                else
                    menu.AddDisabledItem(new GUIContent("Create/Segment From Project Selection"));
            }
            else if (IsNotifyTrack(local))
            {
                menu.AddItem(new GUIContent("Create/Notify..."), false, () => OpenCreatePicker(PendingCreateKind.Notify, time));
                if (Selection.activeObject is AnimNotifySO selectedNotify)
                    menu.AddItem(new GUIContent("Create/Notify From Project Selection"), false, () => AddNotifyAtTime(time, selectedNotify));
                else
                    menu.AddDisabledItem(new GUIContent("Create/Notify From Project Selection"));
            }
            else if (IsNotifyStateTrack(local))
            {
                menu.AddItem(new GUIContent("Create/Notify State..."), false, () => OpenCreatePicker(PendingCreateKind.NotifyState, time));
                if (Selection.activeObject is AnimNotifyStateSO selectedState)
                    menu.AddItem(new GUIContent("Create/Notify State From Project Selection"), false, () => AddNotifyStateAtTime(time, selectedState));
                else
                    menu.AddDisabledItem(new GUIContent("Create/Notify State From Project Selection"));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Create"));
            }

            menu.ShowAsContext();
        }

        private bool IsSegmentTrack(Vector2 local) =>
            local.y >= segmentTrackTop && local.y <= segmentTrackTop + TrackHeight;

        private bool IsNotifyTrack(Vector2 local) =>
            local.y >= notifyTrackTop && local.y <= notifyTrackTop + TrackHeight;

        private bool IsNotifyStateTrack(Vector2 local) =>
            local.y >= notifyStateTrackTop && local.y <= notifyStateTrackTop + TrackHeight;

        private void OpenCreatePicker(PendingCreateKind kind, float time)
        {
            pendingCreateKind = kind;
            pendingCreateTime = time;
            pendingObjectPickerControlId = GetNextObjectPickerControlId(kind);

            switch (kind)
            {
                case PendingCreateKind.Segment:
                    EditorGUIUtility.ShowObjectPicker<AnimationClip>(null, false, string.Empty, pendingObjectPickerControlId);
                    break;

                case PendingCreateKind.Notify:
                    EditorGUIUtility.ShowObjectPicker<AnimNotifySO>(null, false, string.Empty, pendingObjectPickerControlId);
                    break;

                case PendingCreateKind.NotifyState:
                    EditorGUIUtility.ShowObjectPicker<AnimNotifyStateSO>(null, false, string.Empty, pendingObjectPickerControlId);
                    break;
            }
        }

        private int GetNextObjectPickerControlId(PendingCreateKind kind)
        {
            unchecked
            {
                objectPickerSerial++;
                int hash = 17;
                hash = hash * 31 + GetHashCode();
                hash = hash * 31 + (int)kind;
                hash = hash * 31 + objectPickerSerial;
                return hash;
            }
        }

        private float GetSegmentDragValue(DragMode mode, int segmentIndex)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            return mode switch
            {
                DragMode.SegmentMove => segment.StartTime,
                DragMode.SegmentBlendIn => segment.BlendIn,
                DragMode.SegmentBlendOut => segment.EndTime - segment.BlendOut,
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

        private void ApplySegmentBlendIn(int segmentIndex, float time)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            time = SnapToNeighborEdge(time, segmentIndex, preferPrevious: true);
            float blendIn = Snap(Mathf.Clamp(time - segment.StartTime, 0f, MaxBlendDuration(segment)));
            WriteSegmentBlend(segmentIndex, blendIn, segment.BlendOut);
        }

        private void ApplySegmentBlendOut(int segmentIndex, float time)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            time = SnapToNeighborEdge(time, segmentIndex, preferPrevious: false);
            float blendOut = Snap(Mathf.Clamp(segment.EndTime - time, 0f, MaxBlendDuration(segment)));
            WriteSegmentBlend(segmentIndex, segment.BlendIn, blendOut);
        }

        private void WriteSegmentBlend(int segmentIndex, float blendIn, float blendOut)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            float maxBlend = MaxBlendDuration(segment);
            blendIn = Mathf.Clamp(blendIn, 0f, maxBlend);
            blendOut = Mathf.Clamp(blendOut, 0f, maxBlend - blendIn);

            Undo.RecordObject(context.Montage, "Adjust Segment Blend");
            SerializedObject so = new(context.Montage);
            SerializedProperty segmentProperty = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("blendIn").floatValue = blendIn;
            segmentProperty.FindPropertyRelative("blendOut").floatValue = blendOut;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplyTransitionCrossfade(int lowerSegmentIndex, float duration)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || lowerSegmentIndex < 0 || lowerSegmentIndex + 1 >= montage.Segments.Count)
                return;

            MontageSegment from = montage.Segments[lowerSegmentIndex];
            MontageSegment to = montage.Segments[lowerSegmentIndex + 1];
            if (from == null || to == null)
                return;

            float maxDuration = Mathf.Min(MaxBlendDuration(from), MaxBlendDuration(to));
            duration = Snap(Mathf.Clamp(duration, 0f, maxDuration));

            Undo.RecordObject(montage, "Adjust Transition Crossfade");
            SerializedObject so = new(montage);
            SerializedProperty segments = so.FindProperty("segments");
            segments.GetArrayElementAtIndex(lowerSegmentIndex).FindPropertyRelative("blendOut").floatValue = duration;
            segments.GetArrayElementAtIndex(lowerSegmentIndex + 1).FindPropertyRelative("blendIn").floatValue = duration;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private static float MaxBlendDuration(MontageSegment segment) =>
            Mathf.Max(MinSegmentDuration, segment.Duration * 0.5f);

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
                if (other == null)
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

        private float SnapToNeighborEdge(float time, int segmentIndex, bool preferPrevious)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return time;

            float tolerance = MagneticSnapPixels / Mathf.Max(1f, pixelsPerSecond);
            int neighborIndex = preferPrevious ? segmentIndex - 1 : segmentIndex + 1;
            if (neighborIndex < 0 || neighborIndex >= montage.Segments.Count)
                return time;

            MontageSegment neighbor = montage.Segments[neighborIndex];
            if (neighbor == null)
                return time;

            float edge = preferPrevious ? neighbor.EndTime : neighbor.StartTime;
            if (Mathf.Abs(time - edge) > tolerance)
                return time;

            snapGuideTime = edge;
            hasSnapGuide = true;
            return edge;
        }

        private void ApplyNotifyTime(int notifyIndex, float time)
        {
            Undo.RecordObject(context.Montage, "Move Anim Notify");
            context.Montage.Notifies[notifyIndex].Time = Snap(time);
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

        private void AddNotifyAtTime(float time) => AddNotifyAtTime(time, null);

        private void AddNotifyAtTime(float time, AnimNotifySO notify)
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
            element.FindPropertyRelative("trackId").stringValue = "Default";
            so.ApplyModifiedPropertiesWithoutUndo();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedNotify(index);
        }

        private void AddNotifyStateAtTime(float time) => AddNotifyStateAtTime(time, null);

        private void AddNotifyStateAtTime(float time, AnimNotifyStateSO notifyState)
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
            element.FindPropertyRelative("trackId").stringValue = "Default";
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedNotifyState(index);
        }

        private void AddSegmentAtTime(float time, AnimationClip clip)
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
            element.FindPropertyRelative("clip").objectReferenceValue = clip;
            element.FindPropertyRelative("startTime").floatValue = Snap(time);
            element.FindPropertyRelative("playRate").floatValue = 1f;
            element.FindPropertyRelative("blendIn").floatValue = 0f;
            element.FindPropertyRelative("blendOut").floatValue = 0f;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedSegment(index);
        }

        private bool DeleteSelected()
        {
            if (context.Montage == null)
                return false;

            if (context.SelectedSegmentIndex >= 0)
                return DeleteArrayElement("segments", context.SelectedSegmentIndex, "Delete Montage Segment");

            if (context.SelectedNotifyIndex >= 0)
                return DeleteArrayElement("notifies", context.SelectedNotifyIndex, "Delete Anim Notify");

            if (context.SelectedNotifyStateIndex >= 0)
                return DeleteArrayElement("notifyStates", context.SelectedNotifyStateIndex, "Delete Anim Notify State");

            return false;
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

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            segmentLayouts.Clear();
            transitionLayouts.Clear();
            notifyStateLayouts.Clear();

            var painter = ctx.painter2D;
            DrawBackground(painter, rect);

            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            DrawRuler(painter, rect, montage.Length);
            DrawTimeGrid(painter, rect, montage.Length);
            float y = RulerHeight + TrackGap;
            segmentTrackTop = y;
            y = DrawSegmentTrack(painter, rect, y, montage);
            notifyTrackTop = y;
            y = DrawNotifyTrack(painter, rect, y, montage, "Default");
            notifyStateTrackTop = y;
            DrawNotifyStateTrack(painter, rect, y, montage);
            DrawSnapGuide(painter, rect);
            DrawPlayhead(painter, rect);
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

        private float DrawSegmentTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.34f, 0.62f, 0.35f));

            for (int i = 0; i < montage.Segments.Count - 1; i++)
            {
                MontageSegment current = montage.Segments[i];
                MontageSegment next = montage.Segments[i + 1];
                if (current?.Clip == null || next?.Clip == null)
                    continue;

                float crossfade = MontageSegmentBlending.GetCrossfadeDuration(current, next);
                float boundaryX = TimeToX(current.EndTime);
                Rect transitionRect;
                if (crossfade > 0f)
                {
                    float x0 = TimeToX(current.EndTime - crossfade);
                    float x1 = TimeToX(current.EndTime + crossfade);
                    transitionRect = new Rect(x0, y + 4f, Mathf.Max(TransitionHandleWidth, x1 - x0), TrackHeight - 8f);
                    painter.fillColor = TransitionColor;
                    FillRoundedRect(painter, transitionRect, 4f);
                    DrawDiagonalHatch(painter, transitionRect, new Color(1f, 1f, 1f, 0.08f));
                }
                else
                {
                    transitionRect = new Rect(
                        boundaryX - TransitionHandleWidth * 0.5f,
                        y + 5f,
                        TransitionHandleWidth,
                        TrackHeight - 10f);
                    painter.fillColor = new Color(TransitionHandleColor.r, TransitionHandleColor.g, TransitionHandleColor.b, 0.22f);
                    FillRoundedRect(painter, transitionRect, 4f);
                }

                DrawTransitionHandle(painter, boundaryX, y + 4f, TrackHeight - 8f, crossfade > 0f);
                transitionLayouts.Add(new TransitionLayout(i, transitionRect, crossfade));
            }

            for (int i = 0; i < montage.Segments.Count; i++)
            {
                MontageSegment segment = montage.Segments[i];
                if (segment?.Clip == null)
                    continue;

                float x0 = TimeToX(segment.StartTime);
                float x1 = TimeToX(segment.EndTime);
                var body = new Rect(x0, y + 2f, Mathf.Max(4f, x1 - x0), TrackHeight - 4f);
                bool selected = context.SelectedSegmentIndex == i;

                painter.fillColor = selected ? SegmentSelectedColor : SegmentCoreColor;
                FillRoundedRect(painter, body, 3f);
                painter.strokeColor = selected ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.22f);
                painter.lineWidth = selected ? 1.6f : 1f;
                StrokeRoundedRect(painter, body, 3f);

                float blendInWidth = segment.BlendIn > 0f
                    ? Mathf.Clamp(segment.BlendIn * pixelsPerSecond, 3f, body.width * 0.5f)
                    : 0f;
                float blendOutWidth = segment.BlendOut > 0f
                    ? Mathf.Clamp(segment.BlendOut * pixelsPerSecond, 3f, body.width * 0.5f)
                    : 0f;

                var visualBlendInRect = new Rect(body.xMin, body.yMin, blendInWidth, body.height);
                var visualBlendOutRect = new Rect(body.xMax - blendOutWidth, body.yMin, blendOutWidth, body.height);
                float blendHitWidth = Mathf.Min(BlendHandleHitWidth, body.width * 0.45f);
                var blendInHitRect = new Rect(body.xMin, body.yMin, blendHitWidth, body.height);
                var blendOutHitRect = new Rect(body.xMax - blendHitWidth, body.yMin, blendHitWidth, body.height);

                if (blendInWidth > 0f)
                {
                    painter.fillColor = BlendOverlayColor;
                    FillRect(painter, visualBlendInRect);
                    DrawBlendHandle(painter, visualBlendInRect.xMax, body.yMin, body.height, true);
                }
                else
                {
                    DrawBlendHandle(painter, body.xMin + 4f, body.yMin, body.height, false);
                }

                if (blendOutWidth > 0f)
                {
                    painter.fillColor = BlendOverlayColor;
                    FillRect(painter, visualBlendOutRect);
                    DrawBlendHandle(painter, visualBlendOutRect.xMin, body.yMin, body.height, true);
                }
                else
                {
                    DrawBlendHandle(painter, body.xMax - 4f, body.yMin, body.height, false);
                }

                if (body.width > 28f)
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
                    painter.lineWidth = 1f;
                    float midY = body.yMin + body.height * 0.5f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(body.xMin + 6f, midY));
                    painter.LineTo(new Vector2(body.xMax - 6f, midY));
                    painter.Stroke();
                }

                DrawSegmentEdgeTicks(painter, body);

                segmentLayouts.Add(new SegmentLayout(i, body, blendInHitRect, blendOutHitRect));
            }

            return y + TrackHeight + TrackGap;
        }

        private float DrawNotifyTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.62f, 0.72f, 0.22f));
            for (int i = 0; i < montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement placement = montage.Notifies[i];
                if (placement == null || placement.TrackId != trackId)
                    continue;

                Color color = placement.Notify != null ? placement.Notify.EditorColor : new Color(0.4f, 0.8f, 1f);
                DrawDiamond(painter, TimeToX(placement.Time), y + TrackHeight * 0.5f, 7f, color);
            }

            return y + TrackHeight + TrackGap;
        }

        private void DrawNotifyStateTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage)
        {
            DrawTrackRow(painter, rect, y, new Color(0.28f, 0.72f, 0.42f, 0.18f));
            for (int i = 0; i < montage.NotifyStates.Count; i++)
            {
                AnimNotifyStatePlacement placement = montage.NotifyStates[i];
                if (placement?.NotifyState == null)
                    continue;

                float x0 = TimeToX(placement.StartTime);
                float x1 = TimeToX(placement.EndTime);
                var bar = new Rect(x0, y, Mathf.Max(4f, x1 - x0), TrackHeight);
                bool selected = context.SelectedNotifyStateIndex == i;
                painter.fillColor = selected
                    ? placement.NotifyState.EditorColor * new Color(1f, 1f, 1f, 0.85f)
                    : placement.NotifyState.EditorColor * new Color(1f, 1f, 1f, 0.55f);
                FillRoundedRect(painter, bar, 3f);
                notifyStateLayouts.Add(new NotifyStateLayout(i, bar));
            }
        }

        private void DrawTrackRow(Painter2D painter, Rect rect, float y, Color accentColor)
        {
            var row = new Rect(rect.xMin + TrackLabelWidth, y, rect.width - TrackLabelWidth, TrackHeight);
            painter.fillColor = TrackRowColor;
            FillRect(painter, row);

            var labelRect = new Rect(rect.xMin + 2f, y + 1f, TrackLabelWidth - 4f, TrackHeight - 2f);
            painter.fillColor = accentColor;
            FillRoundedRect(painter, labelRect, 3f);
        }

        private static void DrawBlendHandle(Painter2D painter, float x, float y, float height, bool active)
        {
            painter.strokeColor = active ? BlendHandleColor : new Color(1f, 1f, 1f, 0.32f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y + 4f));
            painter.LineTo(new Vector2(x, y + height - 4f));
            painter.Stroke();

            painter.lineWidth = 1f;
            for (int i = -1; i <= 1; i++)
            {
                float gripX = x + i * 3f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(gripX, y + height * 0.36f));
                painter.LineTo(new Vector2(gripX, y + height * 0.64f));
                painter.Stroke();
            }
        }

        private static void DrawTransitionHandle(Painter2D painter, float x, float y, float height, bool active)
        {
            painter.strokeColor = active ? TransitionHandleColor : new Color(TransitionHandleColor.r, TransitionHandleColor.g, TransitionHandleColor.b, 0.45f);
            painter.lineWidth = active ? 2.5f : 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y + 2f));
            painter.LineTo(new Vector2(x, y + height - 2f));
            painter.Stroke();

            float midY = y + height * 0.5f;
            float halfWidth = active ? 8f : 6f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x - halfWidth, midY - 5f));
            painter.LineTo(new Vector2(x, midY));
            painter.LineTo(new Vector2(x - halfWidth, midY + 5f));
            painter.Stroke();

            painter.BeginPath();
            painter.MoveTo(new Vector2(x + halfWidth, midY - 5f));
            painter.LineTo(new Vector2(x, midY));
            painter.LineTo(new Vector2(x + halfWidth, midY + 5f));
            painter.Stroke();
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

        private bool TryHitPlayhead(Vector2 local, out float distance)
        {
            distance = Mathf.Abs(local.x - TimeToX(context.PlayheadTime));
            return distance <= 6f;
        }

        private bool TryHitTransition(Vector2 local, out int lowerSegmentIndex, out float duration)
        {
            lowerSegmentIndex = -1;
            duration = 0f;

            for (int i = transitionLayouts.Count - 1; i >= 0; i--)
            {
                TransitionLayout layout = transitionLayouts[i];
                if (!layout.Rect.Contains(local))
                    continue;

                lowerSegmentIndex = layout.LowerSegmentIndex;
                duration = layout.Duration;
                return true;
            }

            return false;
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
                if (layout.BlendIn.width > 0f && layout.BlendIn.Contains(local))
                {
                    mode = DragMode.SegmentBlendIn;
                    return true;
                }

                if (layout.BlendOut.width > 0f && layout.BlendOut.Contains(local))
                {
                    mode = DragMode.SegmentBlendOut;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMin) <= EdgeHandleWidth)
                {
                    mode = DragMode.SegmentMove;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMax) <= EdgeHandleWidth)
                {
                    mode = DragMode.SegmentBlendOut;
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
            if (context.Montage == null || local.y < notifyTrackTop || local.y > notifyTrackTop + TrackHeight)
                return false;

            float best = 999f;
            for (int i = 0; i < context.Montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement placement = context.Montage.Notifies[i];
                if (placement == null)
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

        private void SetPlayheadFromX(float x) => context.SetPlayhead(XToTime(x));

        private void ClampViewStartTime()
        {
            float visibleDuration = Mathf.Max(0f, (contentRect.width - TrackLabelWidth - ContentPadding) / Mathf.Max(1f, pixelsPerSecond));
            float maxStart = Mathf.Max(0f, (context.Montage?.Length ?? 0f) - visibleDuration * 0.35f);
            viewStartTime = Mathf.Clamp(viewStartTime, 0f, maxStart);
        }

        private float TimeToX(float time) => TrackLabelWidth + ContentPadding + (time - viewStartTime) * pixelsPerSecond;

        private float XToTime(float x) => Mathf.Max(0f, viewStartTime + (x - TrackLabelWidth - ContentPadding) / pixelsPerSecond);

        private static float Snap(float time) => Mathf.Round(time / SnapStep) * SnapStep;
    }
}
