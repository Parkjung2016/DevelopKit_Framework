using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageEditorContext
    {
        public AnimMontageLibrarySO MontageLibrary { get; private set; }
        public AnimMontageSO Montage { get; private set; }
        public float PlayheadTime { get; private set; }
        public float PreviousPlayheadTime { get; private set; }
        public bool IsPlaying { get; private set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public float EffectivePlaybackSpeed => Mathf.Max(0.01f, PlaybackSpeed) * (Montage != null ? Montage.RateScale : 1f);
        public bool Loop { get; set; }
        public GameObject PreviewModel { get; private set; }
        public UnityEngine.Object SelectedObject { get; private set; }

        public int SelectedNotifyIndex { get; private set; } = -1;
        public int SelectedNotifyStateIndex { get; private set; } = -1;
        public int SelectedSegmentIndex { get; private set; } = -1;
        public int SelectedCustomElementIndex { get; private set; } = -1;
        public IReadOnlyCollection<int> SelectedSegmentIndices => selectedSegmentIndices;
        public IReadOnlyCollection<int> SelectedNotifyIndices => selectedNotifyIndices;
        public IReadOnlyCollection<int> SelectedNotifyStateIndices => selectedNotifyStateIndices;
        public IReadOnlyCollection<int> SelectedCustomElementIndices => selectedCustomElementIndices;
        public IReadOnlyCollection<string> SelectedTimelineTrackKeys => selectedTimelineTrackKeys;

        private readonly HashSet<int> selectedSegmentIndices = new();
        private readonly HashSet<int> selectedNotifyIndices = new();
        private readonly HashSet<int> selectedNotifyStateIndices = new();
        private readonly HashSet<int> selectedCustomElementIndices = new();
        private readonly HashSet<string> selectedTimelineTrackKeys = new();

        public event Action Changed;
        public event Action MontageChanged;
        public event Action SelectionChanged;
        public event Action PlayheadChanged;
        public event Action PlaybackStateChanged;

        public void SetMontageLibrary(AnimMontageLibrarySO library)
        {
            if (MontageLibrary == library)
            {
                SelectedObject = library != null ? library : Montage;
                RaiseSelectionChanged();
                return;
            }

            MontageLibrary = library;
            SelectedObject = library != null ? library : Montage;
            if (library != null)
            {
                PreviewModel = library.PreviewModel;
                if (Montage != null && !library.Contains(Montage))
                {
                    SetMontage(null);
                    SelectedObject = library;
                    RaiseSelectionChanged();
                }
                else
                {
                    RaiseChanged();
                    RaiseSelectionChanged();
                }
            }
            else
            {
                RaiseChanged();
                RaiseSelectionChanged();
            }
        }

        public void SetMontage(AnimMontageSO montage)
        {
            Montage = montage;
            PlayheadTime = 0f;
            PreviousPlayheadTime = 0f;
            SetPlaying(false);
            ClearTimelineSelection();
            SelectedObject = montage;
            RaiseMontageChanged();
            RaiseChanged();
            RaiseSelectionChanged();
            RaisePlayheadChanged();
        }

        public void SetPreviewModel(GameObject previewModel)
        {
            if (PreviewModel == previewModel)
                return;

            PreviewModel = previewModel;
            RaiseChanged();
        }

        public void SetPlayhead(float time)
        {
            if (Montage == null)
            {
                PreviousPlayheadTime = PlayheadTime;
                PlayheadTime = 0f;
                RaisePlayheadChanged();
                return;
            }

            PreviousPlayheadTime = PlayheadTime;
            PlayheadTime = Mathf.Clamp(time, 0f, Montage.Length);
            RaisePlayheadChanged();
        }

        public void SetSelected(UnityEngine.Object selected)
        {
            SelectedObject = selected;
            ClearTimelineSelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedSegment(int segmentIndex, bool additive = false, bool toggle = false)
        {
            SetTimelineIndexSelection(selectedSegmentIndices, segmentIndex, additive, toggle);
            selectedNotifyIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedCustomElementIndices.Clear();
            selectedTimelineTrackKeys.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedSegments(IEnumerable<int> segmentIndices, bool additive = false)
        {
            if (!additive)
                ClearTimelineSelection();

            foreach (int index in segmentIndices)
                selectedSegmentIndices.Add(index);

            selectedNotifyIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedCustomElementIndices.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedNotify(int notifyIndex, bool additive = false, bool toggle = false)
        {
            SetTimelineIndexSelection(selectedNotifyIndices, notifyIndex, additive, toggle);
            selectedSegmentIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedCustomElementIndices.Clear();
            selectedTimelineTrackKeys.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedNotifies(IEnumerable<int> notifyIndices, bool additive = false)
        {
            if (!additive)
                ClearTimelineSelection();

            foreach (int index in notifyIndices)
                selectedNotifyIndices.Add(index);

            selectedSegmentIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedCustomElementIndices.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedNotifyState(int notifyStateIndex, bool additive = false, bool toggle = false)
        {
            SetTimelineIndexSelection(selectedNotifyStateIndices, notifyStateIndex, additive, toggle);
            selectedSegmentIndices.Clear();
            selectedNotifyIndices.Clear();
            selectedCustomElementIndices.Clear();
            selectedTimelineTrackKeys.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedNotifyStates(IEnumerable<int> notifyStateIndices, bool additive = false)
        {
            if (!additive)
                ClearTimelineSelection();

            foreach (int index in notifyStateIndices)
                selectedNotifyStateIndices.Add(index);

            selectedSegmentIndices.Clear();
            selectedNotifyIndices.Clear();
            selectedCustomElementIndices.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedCustomElement(int customElementIndex, bool additive = false, bool toggle = false)
        {
            SetTimelineIndexSelection(selectedCustomElementIndices, customElementIndex, additive, toggle);
            selectedSegmentIndices.Clear();
            selectedNotifyIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedTimelineTrackKeys.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedCustomElements(IEnumerable<int> customElementIndices, bool additive = false)
        {
            if (!additive)
                ClearTimelineSelection();

            foreach (int index in customElementIndices)
                selectedCustomElementIndices.Add(index);

            selectedSegmentIndices.Clear();
            selectedNotifyIndices.Clear();
            selectedNotifyStateIndices.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedTimelineTrack(string trackKey, bool additive = false, bool toggle = false)
        {
            if (!additive && !toggle)
                ClearTimelineSelection();

            if (toggle && selectedTimelineTrackKeys.Contains(trackKey))
                selectedTimelineTrackKeys.Remove(trackKey);
            else
                selectedTimelineTrackKeys.Add(trackKey);

            selectedSegmentIndices.Clear();
            selectedNotifyIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedCustomElementIndices.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedTimelineTracks(IEnumerable<string> trackKeys, bool additive = false)
        {
            if (!additive)
                ClearTimelineSelection();

            foreach (string key in trackKeys)
                selectedTimelineTrackKeys.Add(key);

            selectedSegmentIndices.Clear();
            selectedNotifyIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedCustomElementIndices.Clear();
            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public void SetSelectedTimelineElements(
            IEnumerable<int> segmentIndices,
            IEnumerable<int> notifyIndices,
            IEnumerable<int> notifyStateIndices,
            IEnumerable<int> customElementIndices,
            IEnumerable<string> trackKeys,
            bool additive = false)
        {
            if (!additive)
                ClearTimelineSelection();

            foreach (int index in segmentIndices)
                selectedSegmentIndices.Add(index);

            foreach (int index in notifyIndices)
                selectedNotifyIndices.Add(index);

            foreach (int index in notifyStateIndices)
                selectedNotifyStateIndices.Add(index);

            foreach (int index in customElementIndices)
                selectedCustomElementIndices.Add(index);

            foreach (string key in trackKeys)
                selectedTimelineTrackKeys.Add(key);

            SelectedObject = Montage;
            SyncLegacySelection();
            RaiseSelectionChanged();
        }

        public bool IsSegmentSelected(int index) => selectedSegmentIndices.Contains(index);
        public bool IsNotifySelected(int index) => selectedNotifyIndices.Contains(index);
        public bool IsNotifyStateSelected(int index) => selectedNotifyStateIndices.Contains(index);
        public bool IsCustomElementSelected(int index) => selectedCustomElementIndices.Contains(index);
        public bool IsTimelineTrackSelected(string trackKey) => selectedTimelineTrackKeys.Contains(trackKey);

        public void SetPlaying(bool playing)
        {
            if (IsPlaying == playing)
                return;

            IsPlaying = playing;
            PlaybackStateChanged?.Invoke();
        }

        public void MarkDirty()
        {
            if (Montage != null)
                EditorUtility.SetDirty(Montage);

            RaiseChanged();
        }

        public void NotifyExternalChange() => RaiseChanged();

        public void NotifyUndoRedo()
        {
            if (Montage == null)
            {
                ClearTimelineSelection();
                SelectedObject = null;
                PlayheadTime = 0f;
                PreviousPlayheadTime = 0f;
            }
            else
            {
                ClampTimelineSelection();
                PlayheadTime = Mathf.Clamp(PlayheadTime, 0f, Montage.Length);
                PreviousPlayheadTime = Mathf.Clamp(PreviousPlayheadTime, 0f, Montage.Length);
                if (SelectedObject == null)
                    SelectedObject = Montage;
            }

            RaiseChanged();
            RaiseSelectionChanged();
            RaisePlayheadChanged();
        }

        private static void SetTimelineIndexSelection(HashSet<int> selection, int index, bool additive, bool toggle)
        {
            if (!additive && !toggle)
                selection.Clear();

            if (toggle && selection.Contains(index))
                selection.Remove(index);
            else
                selection.Add(index);
        }

        private void ClearTimelineSelection()
        {
            selectedSegmentIndices.Clear();
            selectedNotifyIndices.Clear();
            selectedNotifyStateIndices.Clear();
            selectedCustomElementIndices.Clear();
            selectedTimelineTrackKeys.Clear();
            SyncLegacySelection();
        }

        private void ClampTimelineSelection()
        {
            RemoveInvalidIndices(selectedSegmentIndices, Montage.Segments.Count);
            RemoveInvalidIndices(selectedNotifyIndices, Montage.Notifies.Count);
            RemoveInvalidIndices(selectedNotifyStateIndices, Montage.NotifyStates.Count);
            RemoveInvalidIndices(selectedCustomElementIndices, Montage.CustomElements.Count);
            SyncLegacySelection();
        }

        private static void RemoveInvalidIndices(HashSet<int> selection, int count)
        {
            selection.RemoveWhere(index => index < 0 || index >= count);
        }

        private void SyncLegacySelection()
        {
            SelectedSegmentIndex = FirstOrMinusOne(selectedSegmentIndices);
            SelectedNotifyIndex = FirstOrMinusOne(selectedNotifyIndices);
            SelectedNotifyStateIndex = FirstOrMinusOne(selectedNotifyStateIndices);
            SelectedCustomElementIndex = FirstOrMinusOne(selectedCustomElementIndices);
        }

        private static int FirstOrMinusOne(HashSet<int> values)
        {
            foreach (int value in values)
                return value;

            return -1;
        }

        private void RaiseChanged() => Changed?.Invoke();
        private void RaiseMontageChanged() => MontageChanged?.Invoke();
        private void RaiseSelectionChanged() => SelectionChanged?.Invoke();
        private void RaisePlayheadChanged() => PlayheadChanged?.Invoke();
    }
}