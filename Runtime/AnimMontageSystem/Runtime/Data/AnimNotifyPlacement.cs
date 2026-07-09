using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class AnimNotifyPlacement
    {
        [SerializeField] private float time;
        [SerializeReference]
        [SerializeField] private AnimNotify notify;
        [SerializeField] private string trackId = "Default";
        [SerializeField] private float triggerWeightThreshold;
        [SerializeField] private Color customColor = Color.clear;

        public float Time
        {
            get => time;
            set => time = Mathf.Max(0f, value);
        }

        public AnimNotify Notify
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
        public Color CustomColor => customColor;
        public bool HasCustomColor => customColor.a > 0f;
    }
}
