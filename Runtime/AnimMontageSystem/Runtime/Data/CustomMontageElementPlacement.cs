using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class CustomMontageElementPlacement
    {
        [SerializeField] private float startTime;
        [SerializeField] private float endTime;
        [SerializeField] private string trackId = "Default";
        [SerializeReference]
        [SerializeField] private MontageTimelineElement element;
        [SerializeField] private Color customColor = Color.clear;

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

        public string TrackId
        {
            get => string.IsNullOrEmpty(trackId) ? "Default" : trackId;
            set => trackId = string.IsNullOrEmpty(value) ? "Default" : value;
        }

        public MontageTimelineElement Element
        {
            get => element;
            set => element = value;
        }

        public float Duration => Mathf.Max(0f, endTime - startTime);
        public Color CustomColor => customColor;
        public bool HasCustomColor => customColor.a > 0f;
    }
}
