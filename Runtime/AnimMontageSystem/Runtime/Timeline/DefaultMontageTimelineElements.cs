using System.Runtime.CompilerServices;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class TransformOffsetMontageElement : MontageTimelineElement<TimelineControlMontageTrack>, IMontageTransformOffsetElement
    {
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffsetEuler;
        [SerializeField] private Vector3 scaleOffset;

        public override string DisplayName => "Transform Offset";
        public override Color EditorColor => new(0.38f, 0.68f, 1f, 0.95f);
        public override float DefaultDuration => 0.5f;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;
        public Vector3 ScaleOffset => scaleOffset;
    }

    [System.Serializable]
    public sealed class PlaybackSpeedMontageElement : MontageTimelineElement<TimelineControlMontageTrack>, IMontagePlaybackSpeedElement
    {
        [SerializeField] private float speedMultiplier = 0.5f;

        public override string DisplayName => "Playback Speed";
        public override Color EditorColor => new(0.74f, 0.95f, 0.46f, 0.95f);
        public override float DefaultDuration => 0.5f;
        public float PlaybackSpeedMultiplier => Mathf.Max(0f, speedMultiplier);
    }

    [System.Serializable]
    public sealed class CameraShakeMarkerMontageElement : MontageTimelineElement<TimelineControlMontageTrack>
    {
        [SerializeField] private float amplitude = 1f;
        [SerializeField] private float frequency = 12f;

        public override string DisplayName => "Camera Shake Marker";
        public override Color EditorColor => new(1f, 0.48f, 0.72f, 0.95f);
        public override float DefaultDuration => 0.15f;
        public float Amplitude => amplitude;
        public float Frequency => frequency;
    }

    [System.Serializable]
    public sealed class TimeControlMontageElement : MontageTimelineElement<TimelineControlMontageTrack>, IMontageTimelineElementBehaviour, IMontageTimeScaleElement
    {
        [SerializeField] private float timeScale = 0.2f;
        [SerializeField] private int priority = 100;
        [SerializeField] private string layerKey;

        public override string DisplayName => "Time Control";
        public override Color EditorColor => new(0.39f, 0.58f, 0.92f, 0.95f);
        public override float DefaultDuration => 0.15f;
        public float TimeScale => Mathf.Max(0f, timeScale);
        public float TimeScaleMultiplier => TimeScale;
        public int Priority => priority;

        public void OnElementEnter(MontageTimelineElementContext context) => ApplyLayer(context);

        public void OnElementUpdate(MontageTimelineElementContext context) => ApplyLayer(context);

        public void OnElementExit(MontageTimelineElementContext context)
        {
            TimeScaleLayerManager.Instance.RemoveLayer(GetLayerKey(context));
        }

        private void ApplyLayer(MontageTimelineElementContext context)
        {
            TimeScaleLayerManager.Instance.SetLayer(GetLayerKey(context), TimeScale, priority);
        }

        private string GetLayerKey(MontageTimelineElementContext context)
        {
            if (!string.IsNullOrWhiteSpace(layerKey))
                return layerKey;

            int playerId = context.Player != null ? RuntimeHelpers.GetHashCode(context.Player) : 0;
            return $"AnimMontage.TimeControl.{playerId}.{context.PlacementIndex}";
        }
    }
}