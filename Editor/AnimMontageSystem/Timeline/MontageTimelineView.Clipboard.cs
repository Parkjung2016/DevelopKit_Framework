using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed partial class MontageTimelineView
    {
        private bool CopySelectionToClipboard()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return false;

            ClearClipboard();

            bool hasElementSelection = context.SelectedSegmentIndices.Count > 0
                || context.SelectedNotifyIndices.Count > 0
                || context.SelectedNotifyStateIndices.Count > 0;

            if (hasElementSelection)
            {
                SerializedObject so = new(montage);
                CopySelectedSegments(so.FindProperty("segments"));
                CopySelectedNotifies(so.FindProperty("notifies"));
                CopySelectedNotifyStates(so.FindProperty("notifyStates"));
                clipboardKind = copiedSegments.Count > 0 || copiedNotifies.Count > 0 || copiedNotifyStates.Count > 0
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
                    (MontageSegmentType)(segment.FindPropertyRelative("segmentType")?.enumValueIndex ?? 0),
                    segment.FindPropertyRelative("clip")?.objectReferenceValue as AnimationClip,
                    segment.FindPropertyRelative("startTime")?.floatValue ?? 0f,
                    segment.FindPropertyRelative("emptyStateDuration")?.floatValue ?? 1f,
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
                trackId = CreateUniqueTrackId(tracks, $"{source.TrackId} Copy");
                track.stringValue = trackId;

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

            PasteSegments(so.FindProperty("segments"), pasteTarget, timeOffset, newSegments);
            PasteNotifies(so.FindProperty("notifies"), pasteTarget, timeOffset, newNotifies);
            PasteNotifyStates(so.FindProperty("notifyStates"), pasteTarget, timeOffset, newStates);

            if (newSegments.Count == 0 && newNotifies.Count == 0 && newStates.Count == 0)
                return false;

            so.ApplyModifiedProperties();
            context.MarkDirty();
            context.SetSelectedTimelineElements(newSegments, newNotifies, newStates, Array.Empty<string>());
            MarkDirtyRepaint();
            return true;
        }

        private bool HasCopiedTimelineElements() =>
            copiedSegments.Count > 0
            || copiedNotifies.Count > 0
            || copiedNotifyStates.Count > 0;

        private float GetCopiedElementsMinTime()
        {
            float minTime = float.PositiveInfinity;

            for (int i = 0; i < copiedSegments.Count; i++)
                minTime = Mathf.Min(minTime, copiedSegments[i].StartTime);

            for (int i = 0; i < copiedNotifies.Count; i++)
                minTime = Mathf.Min(minTime, copiedNotifies[i].Time);

            for (int i = 0; i < copiedNotifyStates.Count; i++)
                minTime = Mathf.Min(minTime, copiedNotifyStates[i].StartTime);

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
                segment.FindPropertyRelative("segmentType").enumValueIndex = (int)source.SegmentType;
                segment.FindPropertyRelative("clip").objectReferenceValue = source.Clip;
                segment.FindPropertyRelative("startTime").floatValue = Snap(Mathf.Max(0f, source.StartTime + timeOffset));
                segment.FindPropertyRelative("emptyStateDuration").floatValue = source.EmptyStateDuration;
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
    }
}