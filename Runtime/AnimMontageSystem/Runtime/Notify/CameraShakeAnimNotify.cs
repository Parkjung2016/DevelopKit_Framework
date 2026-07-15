using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class CameraShakeAnimNotify : AnimNotify
    {
        [SerializeField, Min(0f)] private float strength = 1f;
        [SerializeField, Min(0.01f)] private float duration = 0.2f;
        [SerializeField] private Vector3 direction = Vector3.down;
        [SerializeField] private MontageCameraImpulseShape shape = MontageCameraImpulseShape.Bump;

        public override string DisplayName => "Camera Shake Impulse";
        public override Color EditorColor => new(1f, 0.38f, 0.64f, 0.95f);
        public float Strength => Mathf.Max(0f, strength);
        public float Duration => Mathf.Max(0.01f, duration);
        public Vector3 Direction => direction;
        public MontageCameraImpulseShape Shape => shape;

        public override void OnNotify(AnimNotifyContext context)
        {
            if (!Application.isPlaying)
                return;

            MontageCameraShakeRuntime.GenerateImpulse(
                context,
                new MontageCameraImpulseSettings(Strength, Duration, Direction, Shape));
        }
    }
}
