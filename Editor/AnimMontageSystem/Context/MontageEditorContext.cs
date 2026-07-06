using System;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageEditorContext
    {
        public AnimMontageSO Montage { get; private set; }
        public float PlayheadTime { get; private set; }
        public bool IsPlaying { get; private set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public bool Loop { get; set; }
        public GameObject PreviewModel { get; set; }
        public UnityEngine.Object SelectedObject { get; private set; }

        public int SelectedNotifyIndex { get; private set; } = -1;
        public int SelectedNotifyStateIndex { get; private set; } = -1;
        public int SelectedSegmentIndex { get; private set; } = -1;

        public event Action Changed;
        public event Action SelectionChanged;
        public event Action PlayheadChanged;
        public event Action PlaybackStateChanged;

        public void SetMontage(AnimMontageSO montage)
        {
            Montage = montage;
            PlayheadTime = 0f;
            SetPlaying(false);
            SelectedNotifyIndex = -1;
            SelectedNotifyStateIndex = -1;
            SelectedSegmentIndex = -1;
            SelectedObject = montage;
            RaiseChanged();
            RaiseSelectionChanged();
            RaisePlayheadChanged();
        }

        public void SetPlayhead(float time)
        {
            if (Montage == null)
            {
                PlayheadTime = 0f;
                RaisePlayheadChanged();
                return;
            }

            PlayheadTime = Mathf.Clamp(time, 0f, Montage.Length);
            RaisePlayheadChanged();
        }

        public void SetSelected(UnityEngine.Object selected)
        {
            SelectedObject = selected;
            SelectedNotifyIndex = -1;
            SelectedNotifyStateIndex = -1;
            SelectedSegmentIndex = -1;
            RaiseSelectionChanged();
        }

        public void SetSelectedSegment(int segmentIndex)
        {
            SelectedSegmentIndex = segmentIndex;
            SelectedNotifyIndex = -1;
            SelectedNotifyStateIndex = -1;
            SelectedObject = Montage;
            RaiseSelectionChanged();
        }

        public void SetSelectedNotify(int notifyIndex)
        {
            SelectedNotifyIndex = notifyIndex;
            SelectedNotifyStateIndex = -1;
            SelectedSegmentIndex = -1;
            SelectedObject = Montage;
            RaiseSelectionChanged();
        }

        public void SetSelectedNotifyState(int notifyStateIndex)
        {
            SelectedNotifyStateIndex = notifyStateIndex;
            SelectedNotifyIndex = -1;
            SelectedSegmentIndex = -1;
            SelectedObject = Montage;
            RaiseSelectionChanged();
        }

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

        private void RaiseChanged() => Changed?.Invoke();
        private void RaiseSelectionChanged() => SelectionChanged?.Invoke();
        private void RaisePlayheadChanged() => PlayheadChanged?.Invoke();
    }
}
