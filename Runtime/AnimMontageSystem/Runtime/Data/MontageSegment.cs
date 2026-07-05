using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class MontageSegment
    {
        [SerializeField] private string sectionName = "Default";
        [SerializeField] private AnimationClip clip;
        [SerializeField] private float startTime;
        [SerializeField] private float playRate = 1f;
        [SerializeField] private float blendIn;
        [SerializeField] private float blendOut;

        public string SectionName => sectionName;
        public AnimationClip Clip => clip;
        public float StartTime
        {
            get => startTime;
            set => startTime = Mathf.Max(0f, value);
        }
        public float PlayRate => playRate <= 0f ? 1f : playRate;
        public float BlendIn => Mathf.Max(0f, blendIn);
        public float BlendOut => Mathf.Max(0f, blendOut);

        public float Duration => Clip != null ? Clip.length / PlayRate : 0f;
        public float EndTime => StartTime + Duration;

        public bool ContainsTime(float montageTime) =>
            montageTime >= StartTime && montageTime < EndTime;

        public float ToClipTime(float montageTime)
        {
            float local = (montageTime - StartTime) * PlayRate;
            if (Clip == null)
                return 0f;

            return Mathf.Clamp(local, 0f, Clip.length);
        }
    }
}
