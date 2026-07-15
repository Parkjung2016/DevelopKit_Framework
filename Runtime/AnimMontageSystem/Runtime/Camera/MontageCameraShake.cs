using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public enum MontageCameraImpulseShape
    {
        Recoil,
        Bump,
        Explosion,
        Rumble
    }

    public readonly struct MontageCameraShakeSettings
    {
        public MontageCameraShakeSettings(float amplitude, float frequency)
        {
            Amplitude = Mathf.Max(0f, amplitude);
            Frequency = Mathf.Max(0f, frequency);
        }

        public float Amplitude { get; }
        public float Frequency { get; }
    }

    public readonly struct MontageCameraImpulseSettings
    {
        public MontageCameraImpulseSettings(
            float strength,
            float duration,
            Vector3 direction,
            MontageCameraImpulseShape shape)
        {
            Strength = Mathf.Max(0f, strength);
            Duration = Mathf.Max(0.01f, duration);
            Direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.down;
            Shape = shape;
        }

        public float Strength { get; }
        public float Duration { get; }
        public Vector3 Direction { get; }
        public MontageCameraImpulseShape Shape { get; }
    }

    public interface IMontageCameraShakeProvider
    {
        void Set(long requestId, MontageCameraShakeSettings settings);
        void Remove(long requestId);
        void GenerateImpulse(GameObject source, MontageCameraImpulseSettings settings);
        void Reset();
    }

    public static class MontageCameraShakeRuntime
    {
        private static IMontageCameraShakeProvider provider;

        public static IMontageCameraShakeProvider Provider
        {
            get => provider;
            set
            {
                if (ReferenceEquals(provider, value))
                    return;

                provider?.Reset();
                provider = value;
            }
        }

        public static long CreateRequestId(AnimNotifyState state, AnimNotifyContext context)
        {
            GameObject owner = AnimNotifyRuntimeUtility.GetOwnerKey(context);
            int ownerId = owner != null ? owner.GetEntityId().GetHashCode() : 0;
            int stateId = state != null ? RuntimeHelpers.GetHashCode(state) : 0;
            return ((long)ownerId << 32) | (uint)stateId;
        }

        public static void Set(long requestId, MontageCameraShakeSettings settings) =>
            provider?.Set(requestId, settings);

        public static void Remove(long requestId) => provider?.Remove(requestId);

        public static void GenerateImpulse(AnimNotifyContext context, MontageCameraImpulseSettings settings)
        {
            GameObject source = AnimNotifyRuntimeUtility.GetOwnerKey(context);
            if (source != null)
                provider?.GenerateImpulse(source, settings);
        }

        public static void Reset()
        {
            provider?.Reset();
            provider = null;
        }
    }

    public static class MontageCameraShakeSampler
    {
        public static readonly Vector3 PositionAmplitude = new(0.025f, 0.02f, 0.01f);
        public static readonly Vector3 RotationAmplitude = new(0.75f, 0.9f, 0.35f);

        private static readonly AnimationCurve RecoilCurve = new(new[]
        {
            new Keyframe(0f, 1f, -3.2f, -3.2f),
            new Keyframe(1f, 0f, 0f, 0f)
        });

        private static readonly AnimationCurve BumpCurve = new(new[]
        {
            new Keyframe(0f, 0f, -4.9f, -4.9f),
            new Keyframe(0.2f, 0f, 8.25f, 8.25f),
            new Keyframe(1f, 0f, -0.25f, -0.25f)
        });

        private static readonly AnimationCurve ExplosionCurve = new(new[]
        {
            new Keyframe(0f, -1.4f, -7.9f, -7.9f),
            new Keyframe(0.27f, 0.78f, 23.4f, 23.4f),
            new Keyframe(0.54f, -0.12f, 22.6f, 22.6f),
            new Keyframe(0.75f, 0.042f, 9.23f, 9.23f),
            new Keyframe(0.9f, -0.02f, 5.8f, 5.8f),
            new Keyframe(0.95f, -0.006f, -3f, -3f),
            new Keyframe(1f, 0f, 0f, 0f)
        });

        private static readonly AnimationCurve RumbleCurve = new(new[]
        {
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.1f, 0.25f, 0f, 0f),
            new Keyframe(0.2f, 0f, 0f, 0f),
            new Keyframe(0.3f, 0.75f, 0f, 0f),
            new Keyframe(0.4f, 0f, 0f, 0f),
            new Keyframe(0.5f, 1f, 0f, 0f),
            new Keyframe(0.6f, 0f, 0f, 0f),
            new Keyframe(0.7f, 0.75f, 0f, 0f),
            new Keyframe(0.8f, 0f, 0f, 0f),
            new Keyframe(0.9f, 0.25f, 0f, 0f),
            new Keyframe(1f, 0f, 0f, 0f)
        });

        public static void Evaluate(
            float localTime,
            MontageCameraShakeSettings settings,
            out Vector3 positionOffset,
            out Quaternion rotationOffset)
        {
            if (settings.Amplitude <= 0f || settings.Frequency <= 0f)
            {
                positionOffset = Vector3.zero;
                rotationOffset = Quaternion.identity;
                return;
            }

            float time = Mathf.Max(0f, localTime) * settings.Frequency;
            Vector3 positionNoise = new(
                SampleNoise(time, 13.17f),
                SampleNoise(time, 29.41f),
                SampleNoise(time, 47.73f));
            Vector3 rotationNoise = new(
                SampleNoise(time, 61.09f),
                SampleNoise(time, 83.27f),
                SampleNoise(time, 101.53f));

            positionOffset = Vector3.Scale(positionNoise, PositionAmplitude) * settings.Amplitude;
            Vector3 rotationEuler = Vector3.Scale(rotationNoise, RotationAmplitude) * settings.Amplitude;
            rotationOffset = Quaternion.Euler(rotationEuler);
        }

        public static Vector3 EvaluateImpulse(float localTime, MontageCameraImpulseSettings settings)
        {
            if (settings.Strength <= 0f || localTime < 0f || localTime > settings.Duration)
                return Vector3.zero;

            float normalizedTime = Mathf.Clamp01(localTime / settings.Duration);
            return settings.Direction * settings.Strength * GetImpulseCurve(settings.Shape).Evaluate(normalizedTime);
        }

        private static AnimationCurve GetImpulseCurve(MontageCameraImpulseShape shape) => shape switch
        {
            MontageCameraImpulseShape.Recoil => RecoilCurve,
            MontageCameraImpulseShape.Explosion => ExplosionCurve,
            MontageCameraImpulseShape.Rumble => RumbleCurve,
            _ => BumpCurve
        };

        private static float SampleNoise(float time, float seed) =>
            Mathf.PerlinNoise(time, seed) * 2f - 1f;
    }
}