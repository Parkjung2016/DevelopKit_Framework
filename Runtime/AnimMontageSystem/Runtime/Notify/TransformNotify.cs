using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class TransformNotify : AnimNotify, IMontageTransformOffsetNotify, IMontageDurationNotify
    {
        public override bool CanEditTriggerOnManualPreview() => false;

        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffsetEuler;
        [SerializeField] private Vector3 scaleOffset;
        [SerializeField] private MontageTimelineEasing positionEasing = new();
        [SerializeField] private MontageTimelineEasing rotationEasing = new();
        [SerializeField] private MontageTimelineEasing scaleEasing = new();

        public override string DisplayName => "Transform";
        public override Color EditorColor => new(0.38f, 0.68f, 1f, 0.95f);
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;
        public Vector3 ScaleOffset => scaleOffset;
        public MontageTimelineEasing PositionEasing => positionEasing ??= new MontageTimelineEasing();
        public MontageTimelineEasing RotationEasing => rotationEasing ??= new MontageTimelineEasing();
        public MontageTimelineEasing ScaleEasing => scaleEasing ??= new MontageTimelineEasing();
        public float Duration => Mathf.Max(PositionEasing.Duration, RotationEasing.Duration, ScaleEasing.Duration);

        public override void OnNotify(AnimNotifyContext context)
        {
        }
    }
}