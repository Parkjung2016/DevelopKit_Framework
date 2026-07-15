using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public enum MontageTimelineEasePreset
    {
        Linear = 0,
        EaseIn = 1,
        EaseOut = 2,
        EaseInOut = 3,
        CustomCurve = 4,
        Instant = 5,
        SmootherStep = 6
    }

    [Serializable]
    public sealed class MontageTimelineEasing
    {
        [FormerlySerializedAs("mode")]
        [SerializeField] private MontageTimelineEasePreset preset = MontageTimelineEasePreset.EaseInOut;
        [SerializeField, Min(0f)] private float duration = 0.2f;
        [SerializeField] private AnimationCurve customCurve = CreatePresetCurve(MontageTimelineEasePreset.EaseInOut);

        public MontageTimelineEasePreset Preset => preset;
        public float Duration => Mathf.Max(0f, duration);
        public AnimationCurve Curve => customCurve;

        public float Evaluate(float localTime, float elementDuration)
        {
            float easingDuration = Duration;
            if (easingDuration <= 0.0001f)
                return 1f;

            float normalizedTime = Mathf.Clamp01(localTime / easingDuration);
            return customCurve != null ? customCurve.Evaluate(normalizedTime) : normalizedTime;
        }

        public static AnimationCurve CreatePresetCurve(MontageTimelineEasePreset preset)
        {
            AnimationCurve curve = preset switch
            {
                MontageTimelineEasePreset.Instant => CreateCurve(
                    new Keyframe(0f, 1f, 0f, 0f),
                    new Keyframe(1f, 1f, 0f, 0f)),
                MontageTimelineEasePreset.Linear => CreateCurve(
                    new Keyframe(0f, 0f, 1f, 1f),
                    new Keyframe(1f, 1f, 1f, 1f)),
                MontageTimelineEasePreset.EaseIn => CreateCurve(
                    new Keyframe(0f, 0f, 0f, 0f),
                    new Keyframe(1f, 1f, 2f, 2f)),
                MontageTimelineEasePreset.EaseOut => CreateCurve(
                    new Keyframe(0f, 0f, 2f, 2f),
                    new Keyframe(1f, 1f, 0f, 0f)),
                MontageTimelineEasePreset.EaseInOut => CreateCurve(
                    new Keyframe(0f, 0f, 0f, 0f),
                    new Keyframe(1f, 1f, 0f, 0f)),
                MontageTimelineEasePreset.SmootherStep => CreateCurve(
                    new Keyframe(0f, 0f, 0f, 0f),
                    new Keyframe(0.5f, 0.5f, 1.875f, 1.875f),
                    new Keyframe(1f, 1f, 0f, 0f)),
                _ => null
            };

            return curve;
        }

        private static AnimationCurve CreateCurve(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys)
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.ClampForever
            };
            return curve;
        }
    }
}
