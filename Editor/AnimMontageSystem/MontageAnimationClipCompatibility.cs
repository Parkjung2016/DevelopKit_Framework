using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageAnimationClipCompatibility
    {
        public static bool IsCompatible(GameObject previewModel, AnimationClip clip)
        {
            if (clip == null)
                return false;

            if (previewModel == null)
                return true;

            Animator animator = previewModel.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null)
                return !clip.humanMotion;

            return animator.avatar.isHuman ? clip.humanMotion : !clip.humanMotion;
        }
    }
}