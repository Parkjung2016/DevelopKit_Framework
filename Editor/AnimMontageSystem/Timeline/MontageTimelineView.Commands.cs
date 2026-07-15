using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed partial class MontageTimelineView
    {
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
            bool allowTrackTransfer = dragSegmentStartTimes.Count + dragNotifyTimes.Count + dragNotifyStateRanges.Count <= 1;
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
            if (segment == null)
                return;

            if (segment.IsEmptyState)
            {
                float fixedEndTime = dragAnchorSegmentEnd;
                startTime = SnapTimelineEdgeTime(
                    Mathf.Clamp(startTime, 0f, fixedEndTime - MinSegmentDuration),
                    TrackKind.Segment,
                    segment.TrackId,
                    ignoreSegmentIndex: segmentIndex);
                startTime = Snap(Mathf.Min(startTime, fixedEndTime - MinSegmentDuration));

                Undo.RecordObject(montage, "Resize Empty State Start");
                SerializedObject emptyStateObject = new(montage);
                SerializedProperty emptyState =
                    emptyStateObject.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
                emptyState.FindPropertyRelative("startTime").floatValue = startTime;
                emptyState.FindPropertyRelative("emptyStateDuration").floatValue =
                    Mathf.Max(MinSegmentDuration, fixedEndTime - startTime);
                emptyStateObject.ApplyModifiedProperties();
                context.MarkDirty();
                return;
            }

            if (segment.Clip == null)
                return;

            float playRate = segment.PlayRate;
            float maxStartTime = dragAnchorSegmentEnd - MinSegmentDuration;
            startTime = SnapTimelineEdgeTime(
                Mathf.Clamp(startTime, 0f, maxStartTime),
                TrackKind.Segment,
                segment.TrackId,
                ignoreSegmentIndex: segmentIndex);
            float deltaClip = (startTime - dragAnchorValue) * playRate;
            float clipStart = Mathf.Clamp(
                dragAnchorClipStart + deltaClip,
                0f,
                dragAnchorClipEnd - MinSegmentDuration * playRate);
            float duration = Mathf.Max(MinSegmentDuration, (dragAnchorClipEnd - clipStart) / playRate);

            Undo.RecordObject(montage, "Trim Montage Segment Start");
            SerializedObject so = new(montage);
            SerializedProperty segmentProperty =
                so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("startTime").floatValue =
                dragAnchorSegmentEnd - duration;
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
            if (segment == null)
                return;

            if (segment.IsEmptyState)
            {
                float startTime = Snap(Mathf.Max(0f, segment.StartTime));
                endTime = SnapTimelineEdgeTime(
                    Mathf.Max(startTime + MinSegmentDuration, endTime),
                    TrackKind.Segment,
                    segment.TrackId,
                    ignoreSegmentIndex: segmentIndex);

                Undo.RecordObject(montage, "Resize Empty State End");
                SerializedObject emptyStateObject = new(montage);
                SerializedProperty emptyState =
                    emptyStateObject.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
                emptyState.FindPropertyRelative("emptyStateDuration").floatValue =
                    Mathf.Max(MinSegmentDuration, endTime - startTime);
                emptyStateObject.ApplyModifiedProperties();
                context.MarkDirty();
                return;
            }

            if (segment.Clip == null)
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
            {
                clipEnd = Mathf.Clamp(
                    clipEnd,
                    dragAnchorClipStart + MinSegmentDuration * playRate,
                    segment.Clip.length);
            }
            else
            {
                clipEnd = Mathf.Max(
                    dragAnchorClipStart + MinSegmentDuration * playRate,
                    clipEnd);
            }

            Undo.RecordObject(montage, "Trim Montage Segment End");
            SerializedObject so = new(montage);
            SerializedProperty segmentProperty =
                so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
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
            int ignoreNotifyStateIndex = -1)
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
            float duration = notifyState != null
                ? Mathf.Max(MinSegmentDuration, notifyState.DefaultDuration)
                : DefaultQuickBlendDuration;
            element.FindPropertyRelative("endTime").floatValue = Snap(time + duration);
            element.FindPropertyRelative("notifyState").managedReferenceValue = notifyState;
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            element.FindPropertyRelative("customColor").colorValue = Color.clear;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedNotifyState(index);
        }

        private void AddEmptyStateAtTime(float time, string trackId)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Add Empty Animation State");
            SerializedObject so = new(montage);
            SerializedProperty segments = so.FindProperty("segments");
            int index = segments.arraySize;
            segments.InsertArrayElementAtIndex(index);
            SerializedProperty element = segments.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("sectionName").stringValue = "Empty State";
            element.FindPropertyRelative("trackId").stringValue = SanitizeTrackId(trackId);
            element.FindPropertyRelative("segmentType").enumValueIndex = (int)MontageSegmentType.EmptyState;
            element.FindPropertyRelative("clip").objectReferenceValue = null;
            element.FindPropertyRelative("startTime").floatValue = Snap(time);
            element.FindPropertyRelative("emptyStateDuration").floatValue = 1f;
            element.FindPropertyRelative("clipStartTime").floatValue = 0f;
            element.FindPropertyRelative("clipEndTime").floatValue = 0f;
            element.FindPropertyRelative("playRate").floatValue = 1f;
            element.FindPropertyRelative("blendIn").floatValue = 0f;
            element.FindPropertyRelative("blendOut").floatValue = 0f;
            element.FindPropertyRelative("customColor").colorValue = Color.clear;
            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetPlayhead(time);
            context.SetSelectedSegment(index);
            MarkDirtyRepaint();
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
            element.FindPropertyRelative("segmentType").enumValueIndex = (int)MontageSegmentType.Animation;
            element.FindPropertyRelative("clip").objectReferenceValue = clip;
            element.FindPropertyRelative("startTime").floatValue = Snap(time);
            element.FindPropertyRelative("emptyStateDuration").floatValue = 1f;
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
            if (!CanReplaceSegmentClip(segmentIndex))
                return;

            Undo.RecordObject(montage, "Replace Montage Segment Clip");
            SerializedObject so = new(montage);
            SerializedProperty segment = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segment.FindPropertyRelative("sectionName").stringValue = clip != null ? clip.name : "Default";
            segment.FindPropertyRelative("segmentType").enumValueIndex = (int)MontageSegmentType.Animation;
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

        public bool CanReplaceSelectedSegmentClip() =>
            HasSelectedSegment() && CanReplaceSegmentClip(context.SelectedSegmentIndex);

        private bool CanReplaceSegmentClip(int segmentIndex)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || segmentIndex < 0 || segmentIndex >= montage.Segments.Count)
                return false;

            MontageSegment segment = montage.Segments[segmentIndex];
            return segment != null && !segment.IsEmptyState;
        }

        public bool CanSplitSelectedSegmentAtPlayhead() =>
            HasSelectedSegment() && CanSplitSegmentAtTime(context.SelectedSegmentIndex, context.PlayheadTime);

        public void SplitSelectedSegmentAtPlayhead()
        {
            if (CanSplitSelectedSegmentAtPlayhead())
                SplitSegmentAtTime(context.SelectedSegmentIndex, context.PlayheadTime);
        }

        public void ReplaceSelectedSegmentClip()
        {
            if (!CanReplaceSelectedSegmentClip())
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
            target.FindPropertyRelative("segmentType").enumValueIndex = source.FindPropertyRelative("segmentType").enumValueIndex;
            target.FindPropertyRelative("clip").objectReferenceValue = source.FindPropertyRelative("clip").objectReferenceValue;
            target.FindPropertyRelative("startTime").floatValue = source.FindPropertyRelative("startTime").floatValue;
            target.FindPropertyRelative("emptyStateDuration").floatValue = source.FindPropertyRelative("emptyStateDuration").floatValue;
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
                || context.SelectedNotifyStateIndices.Count > 0)
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
                        string currentTrackId = tracks.GetArrayElementAtIndex(j).stringValue;
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
                    string currentTrackId = tracks.GetArrayElementAtIndex(i).stringValue;
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
                || trackSelection.Count > 0)
                context.SetSelectedTimelineElements(segmentSelection, notifySelection, stateSelection, trackSelection, boxSelectAdditive);
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

            context.SetSelectedTimelineElements(segmentSelection, notifySelection, stateSelection, Array.Empty<string>());
            MarkDirtyRepaint();
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
    }
}
