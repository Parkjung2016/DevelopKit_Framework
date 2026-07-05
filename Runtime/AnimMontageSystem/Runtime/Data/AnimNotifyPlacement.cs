using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class AnimNotifyPlacement
    {
        [SerializeField] private float time;
        [SerializeField] private AnimNotifySO notify;
        [SerializeField] private string trackId = "Default";
        [SerializeField] private float triggerWeightThreshold;

        public float Time
        {
            get => time;
            set => time = Mathf.Max(0f, value);
        }

        public AnimNotifySO Notify
        {
            get => notify;
            set => notify = value;
        }

        public string TrackId
        {
            get => string.IsNullOrEmpty(trackId) ? "Default" : trackId;
            set => trackId = value;
        }

        public float TriggerWeightThreshold => triggerWeightThreshold;
    }
}
