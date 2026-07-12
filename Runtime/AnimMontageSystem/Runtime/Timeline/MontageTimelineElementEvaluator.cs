using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct MontageTimelineElementEvaluation
    {
        public MontageTimelineElementEvaluation(float speedMultiplier, Vector3 positionOffset,
            Quaternion rotationOffset, Vector3 scaleOffset)
        {
            SpeedMultiplier = speedMultiplier;
            PositionOffset = positionOffset;
            RotationOffset = rotationOffset;
            ScaleOffset = scaleOffset;
        }
        
        public float SpeedMultiplier { get; }
        public Vector3 PositionOffset { get; }
        public Quaternion RotationOffset { get; }
        public Vector3 ScaleOffset { get; }

        public static MontageTimelineElementEvaluation Default =>
            new( 1f, Vector3.zero, Quaternion.identity, Vector3.zero);
    }

    public static class MontageTimelineElementEvaluator
    {
        public static MontageTimelineElementEvaluation Evaluate(AnimMontageSO montage, float montageTime)
        {
            if (montage == null)
                return MontageTimelineElementEvaluation.Default;

            float speed = 1f;
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
                speed *= Mathf.Max(0f, element.PlaybackSpeedMultiplier);
                position += element.PositionOffset;
                rotation *= Quaternion.Euler(element.RotationOffsetEuler);
                scale += element.ScaleOffset;
            }

            return new MontageTimelineElementEvaluation(speed, position, rotation, scale);
        }
    }
}