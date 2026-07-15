using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public interface IMontageDurationNotify
    {
        float Duration { get; }
    }

    public interface IMontageTransformOffsetNotify
    {
        Vector3 PositionOffset { get; }
        Vector3 RotationOffsetEuler { get; }
        Vector3 ScaleOffset { get; }
        MontageTimelineEasing PositionEasing { get; }
        MontageTimelineEasing RotationEasing { get; }
        MontageTimelineEasing ScaleEasing { get; }
    }

    public interface IMontagePlaybackSpeedNotifyState
    {
        float PlaybackSpeedMultiplier { get; }
    }

    public interface IMontageTimeScaleNotifyState
    {
        float TimeScaleMultiplier { get; }
    }

    public readonly struct MontageNotifyEvaluation
    {
        public MontageNotifyEvaluation(float speedMultiplier, float timeScaleMultiplier, Vector3 positionOffset,
            Quaternion rotationOffset, Vector3 scaleOffset)
        {
            SpeedMultiplier = speedMultiplier;
            TimeScaleMultiplier = timeScaleMultiplier;
            PositionOffset = positionOffset;
            RotationOffset = rotationOffset;
            ScaleOffset = scaleOffset;
        }

        public float SpeedMultiplier { get; }
        public float TimeScaleMultiplier { get; }
        public Vector3 PositionOffset { get; }
        public Quaternion RotationOffset { get; }
        public Vector3 ScaleOffset { get; }

        public static MontageNotifyEvaluation Default =>
            new(1f, 1f, Vector3.zero, Quaternion.identity, Vector3.zero);
    }

    public static class MontageNotifyEvaluator
    {
        public static MontageNotifyEvaluation Evaluate(AnimMontageSO montage, float montageTime)
        {
            if (montage == null)
                return MontageNotifyEvaluation.Default;

            float speed = 1f;
            float timeScale = 1f;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.zero;

            var notifies = montage.Notifies;
            for (int i = 0; i < notifies.Count; i++)
            {
                AnimNotifyPlacement placement = notifies[i];
                if (placement?.Notify is not IMontageTransformOffsetNotify transformNotify
                    || montageTime < placement.Time)
                {
                    continue;
                }

                float localTime = montageTime - placement.Time;
                float positionWeight = transformNotify.PositionEasing.Evaluate(localTime, transformNotify.PositionEasing.Duration);
                float rotationWeight = transformNotify.RotationEasing.Evaluate(localTime, transformNotify.RotationEasing.Duration);
                float scaleWeight = transformNotify.ScaleEasing.Evaluate(localTime, transformNotify.ScaleEasing.Duration);

                position += transformNotify.PositionOffset * positionWeight;
                Quaternion targetRotation = Quaternion.Euler(transformNotify.RotationOffsetEuler);
                rotation *= Quaternion.SlerpUnclamped(Quaternion.identity, targetRotation, rotationWeight);
                scale += transformNotify.ScaleOffset * scaleWeight;
            }

            var states = montage.NotifyStates;
            for (int i = 0; i < states.Count; i++)
            {
                AnimNotifyStatePlacement placement = states[i];
                AnimNotifyState state = placement?.NotifyState;
                if (state == null
                    || montageTime < placement.StartTime
                    || montageTime > placement.EndTime)
                {
                    continue;
                }

                if (state is IMontagePlaybackSpeedNotifyState speedState)
                    speed *= Mathf.Max(0f, speedState.PlaybackSpeedMultiplier);

                if (state is IMontageTimeScaleNotifyState timeScaleState)
                    timeScale *= Mathf.Max(0f, timeScaleState.TimeScaleMultiplier);
            }

            return new MontageNotifyEvaluation(speed, timeScale, position, rotation, scale);
        }
    }
}
