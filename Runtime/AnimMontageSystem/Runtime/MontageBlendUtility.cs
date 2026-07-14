using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    internal static class MontageBlendUtility
    {
        public static float Evaluate(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            return t * t * (3f - 2f * t);
        }
    }
}
