using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class CameraShakeAnimNotifyState : AnimNotifyState
    {
        [SerializeField, Min(0f)] private float amplitude = 1f;
        [SerializeField, Min(0f)] private float frequency = 12f;

        public override string DisplayName => "Camera Shake";
        public override Color EditorColor => new(1f, 0.48f, 0.72f, 0.95f);
        public override float DefaultDuration => 0.15f;
        public float Amplitude => Mathf.Max(0f, amplitude);
        public float Frequency => Mathf.Max(0f, frequency);

        public override void OnBegin(AnimNotifyContext context) => Apply(context);

        public override void OnTick(AnimNotifyContext context, float deltaTime) => Apply(context);

        public override void OnEnd(AnimNotifyContext context)
        {
            if (Application.isPlaying)
                MontageCameraShakeRuntime.Remove(MontageCameraShakeRuntime.CreateRequestId(this, context));
        }

        private void Apply(AnimNotifyContext context)
        {
            if (!Application.isPlaying)
                return;

            MontageCameraShakeRuntime.Set(
                MontageCameraShakeRuntime.CreateRequestId(this, context),
                new MontageCameraShakeSettings(Amplitude, Frequency));
        }
    }
}