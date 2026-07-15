using System.Runtime.CompilerServices;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class TimeControlAnimNotifyState : AnimNotifyState, IMontageTimeScaleNotifyState
    {
        public override bool CanEditTriggerOnManualPreview() => false;
        
        [SerializeField, Min(0f)] private float timeScale = 0.2f;
        [SerializeField] private int priority = 100;
        [SerializeField] private string layerKey;

        public override string DisplayName => "Time Control";
        public override Color EditorColor => new(0.39f, 0.58f, 0.92f, 0.95f);
        public override float DefaultDuration => 0.15f;
        public float TimeScaleMultiplier => Mathf.Max(0f, timeScale);
        public int Priority => priority;

        public override void OnBegin(AnimNotifyContext context) => ApplyLayer(context);

        public override void OnTick(AnimNotifyContext context, float deltaTime) => ApplyLayer(context);

        public override void OnEnd(AnimNotifyContext context)
        {
            if (Application.isPlaying)
                TimeScaleLayerManager.Instance.RemoveLayer(GetLayerKey(context));
        }

        private void ApplyLayer(AnimNotifyContext context)
        {
            if (Application.isPlaying)
                TimeScaleLayerManager.Instance.SetLayer(GetLayerKey(context), TimeScaleMultiplier, priority);
        }

        private string GetLayerKey(AnimNotifyContext context)
        {
            if (!string.IsNullOrWhiteSpace(layerKey))
                return layerKey;

            int ownerId = context.Owner != null ? RuntimeHelpers.GetHashCode(context.Owner) : 0;
            int stateId = RuntimeHelpers.GetHashCode(this);
            return $"AnimMontage.TimeControl.{ownerId}.{stateId}";
        }
    }
}