using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>
    /// Transform Notify의 누적 결과에서 이번 프레임에 적용할 변화량을 계산합니다.
    /// </summary>
    internal sealed class MontageTransformDriver
    {
        private readonly Transform target;
        private MontageNotifyEvaluation previousEvaluation = MontageNotifyEvaluation.Default;

        public MontageTransformDriver(Transform target)
        {
            this.target = target;
        }

        public void Reset() => previousEvaluation = MontageNotifyEvaluation.Default;

        public void Apply(MontageNotifyEvaluation evaluation)
        {
            if (target == null)
                return;

            Vector3 positionDelta = evaluation.PositionOffset - previousEvaluation.PositionOffset;
            Quaternion rotationDelta = Quaternion.Inverse(previousEvaluation.RotationOffset)
                                       * evaluation.RotationOffset;
            Vector3 scaleDelta = evaluation.ScaleOffset - previousEvaluation.ScaleOffset;

            if (positionDelta.sqrMagnitude > 0.0000001f)
                target.position += target.rotation * positionDelta;

            if (Quaternion.Angle(Quaternion.identity, rotationDelta) > 0.0001f)
                target.rotation *= rotationDelta;

            if (scaleDelta.sqrMagnitude > 0.0000001f)
                target.localScale += scaleDelta;

            previousEvaluation = evaluation;
        }
    }
}
