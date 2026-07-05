using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class AnimNotifyStatePlacement
    {
        [SerializeField] private float startTime;
        [SerializeField] private float endTime;
        [SerializeField] private AnimNotifyStateSO notifyState;
        [SerializeField] private string trackId = "Default";

        public float StartTime
        {
            get => startTime;
            set => startTime = Mathf.Max(0f, value);
        }

        public float EndTime
        {
            get => endTime;
            set => endTime = Mathf.Max(startTime, value);
        }

        public AnimNotifyStateSO NotifyState
        {
            get => notifyState;
            set => notifyState = value;
        }

        public string TrackId
        {
            get => string.IsNullOrEmpty(trackId) ? "Default" : trackId;
            set => trackId = value;
        }

        public float Duration => Mathf.Max(0f, endTime - startTime);

        public bool ContainsTime(float montageTime) =>
            montageTime >= startTime && montageTime <= endTime;
    }
}
