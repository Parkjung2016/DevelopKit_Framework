using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>
    /// Montage 레이어의 Blend In과 Blend Out 진행 상태를 관리합니다.
    /// </summary>
    internal sealed class MontageBlendController
    {
        private float playbackElapsedTime;
        private float fadeOutElapsedTime;
        private float fadeOutDuration;
        private float fadeOutStartWeight;

        public bool IsFadingOut { get; private set; }
        public AnimMontageSO FadingOutMontage { get; private set; }
        public float FadeOutMontageTime { get; private set; }

        public void BeginPlayback()
        {
            playbackElapsedTime = 0f;
            ClearFadeOut();
        }

        public void AdvancePlayback(float deltaTime)
        {
            playbackElapsedTime += Mathf.Max(0f, deltaTime);
        }

        public float GetPlaybackWeight(AnimMontageSO montage, float montageTime, float duration)
        {
            if (montage == null)
                return 0f;

            float weight = 1f;
            if (montage.BlendIn > 0f)
            {
                weight = Mathf.Min(
                    weight,
                    MontageBlendUtility.Evaluate(playbackElapsedTime / montage.BlendIn));
            }

            if (montage.BlendOut > 0f && duration > 0f)
            {
                weight = Mathf.Min(
                    weight,
                    MontageBlendUtility.Evaluate((duration - montageTime) / montage.BlendOut));
            }

            return Mathf.Clamp01(weight);
        }

        public void BeginFadeOut(AnimMontageSO montage, float montageTime, float startWeight)
        {
            IsFadingOut = montage != null;
            FadingOutMontage = montage;
            FadeOutMontageTime = montageTime;
            fadeOutElapsedTime = 0f;
            fadeOutDuration = montage != null ? montage.BlendOut : 0f;
            fadeOutStartWeight = Mathf.Clamp01(startWeight);
        }

        public float AdvanceFadeOut(float deltaTime, out bool completed)
        {
            if (!IsFadingOut || FadingOutMontage == null)
            {
                completed = true;
                return 0f;
            }

            fadeOutElapsedTime += Mathf.Max(0f, deltaTime);
            float progress = fadeOutDuration > 0f
                ? Mathf.Clamp01(fadeOutElapsedTime / fadeOutDuration)
                : 1f;
            completed = progress >= 1f;
            return Mathf.Lerp(
                fadeOutStartWeight,
                0f,
                MontageBlendUtility.Evaluate(progress));
        }

        public void ClearFadeOut()
        {
            IsFadingOut = false;
            FadingOutMontage = null;
            FadeOutMontageTime = 0f;
            fadeOutElapsedTime = 0f;
            fadeOutDuration = 0f;
            fadeOutStartWeight = 0f;
        }
    }
}
