using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class TransformOffsetMontageElement : MontageTimelineElement
    {
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffsetEuler;
        [SerializeField] private Vector3 scaleOffset;

        public override string DisplayName => "Transform Offset";
        public override Color EditorColor => new(0.38f, 0.68f, 1f, 0.95f);
        public override float DefaultDuration => 0.5f;
        public override Vector3 PositionOffset => positionOffset;
        public override Vector3 RotationOffsetEuler => rotationOffsetEuler;
        public override Vector3 ScaleOffset => scaleOffset;
    }


    [System.Serializable]
    public sealed class PlaybackSpeedMontageElement : MontageTimelineElement
    {
        [SerializeField] private float speedMultiplier = 0.5f;

        public override string DisplayName => "Playback Speed";
        public override Color EditorColor => new(0.74f, 0.95f, 0.46f, 0.95f);
        public override float DefaultDuration => 0.5f;
        public override float PlaybackSpeedMultiplier => Mathf.Max(0f, speedMultiplier);
    }

    [System.Serializable]
    public sealed class CameraShakeMarkerMontageElement : MontageTimelineElement
    {
        [SerializeField] private float amplitude = 1f;
        [SerializeField] private float frequency = 12f;

        public override string DisplayName => "Camera Shake Marker";
        public override Color EditorColor => new(1f, 0.48f, 0.72f, 0.95f);
        public override float DefaultDuration => 0.15f;
        public float Amplitude => amplitude;
        public float Frequency => frequency;
    }
}