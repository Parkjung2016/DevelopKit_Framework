using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class CustomMontageTrack
    {
        [SerializeField] private string trackId = "Default";
        [SerializeReference]
        [SerializeField] private MontageTimelineTrack trackType;
        [SerializeField] private Color customColor = Color.clear;

        public string TrackId
        {
            get => string.IsNullOrEmpty(trackId) ? "Default" : trackId;
            set => trackId = string.IsNullOrEmpty(value) ? "Default" : value;
        }

        public MontageTimelineTrack TrackType
        {
            get => trackType;
            set => trackType = value;
        }

        public Color CustomColor => customColor;
        public bool HasCustomColor => customColor.a > 0f;
    }
}
