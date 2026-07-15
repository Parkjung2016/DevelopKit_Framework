using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>
    /// 몽타주에서 계산한 루트 모션을 선택한 이동 방식에 맞게 적용합니다.
    /// </summary>
    internal sealed class MontageRootMotionDriver
    {
        private readonly ObjectAnimMontagePlayer player;
        private readonly Transform fallbackTransform;

        private Animator animator;
        private MontageRootMotionMode mode;
        private Rigidbody targetRigidbody;
        private CharacterController targetCharacterController;
        private IMontageRootMotionController customController;

        public MontageRootMotionDriver(ObjectAnimMontagePlayer player, Animator animator)
        {
            this.player = player;
            fallbackTransform = player.transform;
            BindAnimator(animator);
        }

        public Rigidbody Rigidbody => targetRigidbody;
        public CharacterController CharacterController => targetCharacterController;

        public void BindAnimator(Animator value) => animator = value;

        public void Configure(
            MontageRootMotionMode rootMotionMode,
            Rigidbody rigidbody,
            CharacterController characterController,
            IMontageRootMotionController controller)
        {
            mode = rootMotionMode;
            targetRigidbody = rigidbody;
            targetCharacterController = characterController;
            customController = controller;
        }

        public void FindMissingTargets()
        {
            if (targetRigidbody == null)
                targetRigidbody = player.GetComponentInParent<Rigidbody>();

            if (targetCharacterController == null)
                targetCharacterController = player.GetComponentInParent<CharacterController>();
        }

        public void Apply(
            AnimMontageSO montage,
            Vector3 deltaPosition,
            Quaternion deltaRotation,
            float layerWeight)
        {
            MontageRootMotionUtility.Filter(montage, ref deltaPosition, ref deltaRotation);
            deltaRotation = Quaternion.SlerpUnclamped(
                Quaternion.identity,
                deltaRotation,
                Mathf.Clamp01(layerWeight));

            if (!MontageRootMotionUtility.HasDelta(deltaPosition, deltaRotation))
                return;

            switch (mode)
            {
                case MontageRootMotionMode.Rigidbody:
                    ApplyToRigidbody(deltaPosition, deltaRotation);
                    break;
                case MontageRootMotionMode.CharacterController:
                    ApplyToCharacterController(deltaPosition, deltaRotation);
                    break;
                case MontageRootMotionMode.Custom:
                    customController?.ApplyMontageRootMotion(player, animator, deltaPosition, deltaRotation);
                    break;
                default:
                    ApplyToTransform(deltaPosition, deltaRotation);
                    break;
            }
        }

        public void ApplyAnimatorDelta()
        {
            if (animator == null)
                return;

            Transform animatedTransform = animator.transform;
            animatedTransform.position += animator.deltaPosition;
            animatedTransform.rotation *= animator.deltaRotation;
        }

        private void ApplyToTransform(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            fallbackTransform.position += deltaPosition;
            fallbackTransform.rotation *= deltaRotation;
        }

        private void ApplyToRigidbody(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (targetRigidbody == null)
                FindMissingTargets();

            if (targetRigidbody == null)
                return;

            targetRigidbody.MovePosition(targetRigidbody.position + deltaPosition);
            targetRigidbody.MoveRotation(targetRigidbody.rotation * deltaRotation);
        }

        private void ApplyToCharacterController(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (targetCharacterController == null)
                FindMissingTargets();

            if (targetCharacterController == null)
                return;

            targetCharacterController.Move(deltaPosition);
            targetCharacterController.transform.rotation *= deltaRotation;
        }
    }
}
