using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public static class MontageRootMotionUtility
    {
        public static bool IsEnabled(AnimMontageSO montage)
        {
            return montage != null
                   && montage.ApplyRootMotion
                   && (montage.ApplyHorizontalRootMotion
                       || montage.ApplyVerticalRootMotion
                       || montage.ApplyRotationRootMotion);
        }

        public static bool HasDelta(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            return deltaPosition.sqrMagnitude > 0.0000001f
                   || Quaternion.Angle(Quaternion.identity, deltaRotation) > 0.0001f;
        }

        public static void Filter(
            AnimMontageSO montage,
            ref Vector3 deltaPosition,
            ref Quaternion deltaRotation)
        {
            if (!IsEnabled(montage))
            {
                deltaPosition = Vector3.zero;
                deltaRotation = Quaternion.identity;
                return;
            }

            if (!montage.ApplyHorizontalRootMotion)
            {
                deltaPosition.x = 0f;
                deltaPosition.z = 0f;
            }

            if (!montage.ApplyVerticalRootMotion)
                deltaPosition.y = 0f;

            if (!montage.ApplyRotationRootMotion)
                deltaRotation = Quaternion.identity;
            else
                deltaRotation = ExtractYaw(deltaRotation);
        }

        public static Quaternion ExtractYaw(Quaternion rotation)
        {
            var yaw = new Quaternion(0f, rotation.y, 0f, rotation.w);
            float magnitude = Mathf.Sqrt(yaw.y * yaw.y + yaw.w * yaw.w);
            if (magnitude <= 0.00001f)
                return Quaternion.identity;

            float inverseMagnitude = 1f / magnitude;
            yaw.y *= inverseMagnitude;
            yaw.w *= inverseMagnitude;
            return yaw;
        }
    }
}
