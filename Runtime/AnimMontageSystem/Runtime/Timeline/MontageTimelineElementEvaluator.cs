using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct MontageTimelineElementEvaluation
    {
        public MontageTimelineElementEvaluation(float speedMultiplier, float timeScaleMultiplier, Vector3 positionOffset,
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

        public static MontageTimelineElementEvaluation Default =>
            new(1f, 1f, Vector3.zero, Quaternion.identity, Vector3.zero);
    }

    public static class MontageTimelineElementEvaluator
    {
        public static MontageTimelineElementEvaluation Evaluate(AnimMontageSO montage, float montageTime)
        {
            if (montage == null)
                return MontageTimelineElementEvaluation.Default;

            float speed = 1f;
            float timeScale = 1f;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = Vector3.zero;

            var elements = montage.CustomElements;
            for (int i = 0; i < elements.Count; i++)
            {
                CustomMontageElementPlacement placement = elements[i];
                MontageTimelineElement element = placement?.Element;
                if (element == null || montageTime < placement.StartTime || montageTime > placement.EndTime)
                    continue;

                if (element is IMontagePlaybackSpeedElement speedElement)
                    speed *= Mathf.Max(0f, speedElement.PlaybackSpeedMultiplier);

                if (element is IMontageTimeScaleElement timeScaleElement)
                    timeScale *= Mathf.Max(0f, timeScaleElement.TimeScaleMultiplier);

                if (element is IMontageTransformOffsetElement transformElement)
                {
                    position += transformElement.PositionOffset;
                    rotation *= Quaternion.Euler(transformElement.RotationOffsetEuler);
                    scale += transformElement.ScaleOffset;
                }
            }

            return new MontageTimelineElementEvaluation(speed, timeScale, position, rotation, scale);
        }
    }
}