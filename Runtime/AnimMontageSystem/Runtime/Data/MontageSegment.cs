using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class MontageSegment
    {
        [SerializeField] private string sectionName = "Default";
        [SerializeField] private string trackId = "Default";
        [SerializeField] private AnimationClip clip;
        [SerializeField] private float startTime;
        [SerializeField] private float clipStartTime;
        [SerializeField] private float clipEndTime;
        [SerializeField] private float playRate = 1f;
        [SerializeField] private float blendIn;
        [SerializeField] private float blendOut;
        [SerializeField] private Color customColor = Color.clear;

        public string SectionName => sectionName;
        public string TrackId
        {
            get => string.IsNullOrEmpty(trackId) ? "Default" : trackId;
            set => trackId = string.IsNullOrEmpty(value) ? "Default" : value;
        }
        public AnimationClip Clip => clip;
        public float ClipStartTime => Clip != null ? Mathf.Clamp(clipStartTime, 0f, Clip.length) : 0f;
        public bool IsLoopingClip => Clip != null && (Clip.isLooping || Clip.wrapMode == WrapMode.Loop);
        public float ClipEndTime
        {
            get
            {
                if (Clip == null)
                    return 0f;

                float end = clipEndTime <= 0f ? Clip.length : clipEndTime;
                return IsLoopingClip
                    ? Mathf.Max(ClipStartTime, end)
                    : Mathf.Clamp(end, ClipStartTime, Clip.length);
            }
        }
        public float StartTime
        {
            get => startTime;
            set => startTime = Mathf.Max(0f, value);
        }
        public float PlayRate => playRate <= 0f ? 1f : playRate;
        public float BlendIn => Mathf.Max(0f, blendIn);
        public float BlendOut => Mathf.Max(0f, blendOut);
        public Color CustomColor => customColor;
        public bool HasCustomColor => customColor.a > 0f;

        public float LoopEndTime
        {
            get
            {
                if (Clip == null)
                    return 0f;

                float end = clipEndTime <= 0f ? Clip.length : clipEndTime;
                return Mathf.Clamp(end, ClipStartTime, Clip.length);
            }
        }

        public float TrimmedClipDuration => Mathf.Max(0f, ClipEndTime - ClipStartTime);
        public float Duration => Clip != null ? TrimmedClipDuration / PlayRate : 0f;
        public float EndTime => StartTime + Duration;

        public bool ContainsTime(float montageTime) =>
            montageTime >= StartTime && montageTime < EndTime;

        public float ToClipTime(float montageTime)
        {
            return NormalizeClipTime(ToPlayableClipTime(montageTime));
        }

        public float ToPlayableClipTime(float montageTime)
        {
            if (Clip == null)
                return 0f;

            float local = ClipStartTime + (montageTime - StartTime) * PlayRate;
            return IsLoopingClip ? local : Mathf.Clamp(local, ClipStartTime, ClipEndTime);
        }

        public float NormalizeClipTime(float clipTime)
        {
            if (Clip == null)
                return 0f;

            if (!IsLoopingClip)
                return Mathf.Clamp(clipTime, ClipStartTime, ClipEndTime);

            float loopStart = ClipStartTime;
            float loopEnd = Mathf.Max(loopStart + 0.0001f, LoopEndTime);
            float loopLength = Mathf.Max(0.0001f, loopEnd - loopStart);
            return loopStart + Mathf.Repeat(clipTime - loopStart, loopLength);
        }
    }
}
