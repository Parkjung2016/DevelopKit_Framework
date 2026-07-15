using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed partial class MontageTimelineView
    {
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
            HideHoverTooltip();
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
    }
}
