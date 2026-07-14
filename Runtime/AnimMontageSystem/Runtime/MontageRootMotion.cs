using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public enum MontageRootMotionMode
    {
        Transform,
        Rigidbody,
        CharacterController,
        Custom
    }

    /// <summary>
    /// 프로젝트 전용 이동 컴포넌트로 몽타주 루트 모션을 적용할 때 구현합니다.
    /// </summary>
    public interface IMontageRootMotionController
    {
        void ApplyMontageRootMotion(
            ObjectAnimMontagePlayer player,
            Animator animator,
            Vector3 deltaPosition,
            Quaternion deltaRotation);
    }
}
