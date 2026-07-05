using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public sealed class MontagePlaybackState
    {
        public AnimMontageSO Montage { get; private set; }
        public float CurrentTime { get; private set; }
        public float PreviousTime { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }

        public void Begin(AnimMontageSO montage, float startTime)
        {
            Montage = montage;
            CurrentTime = startTime;
            PreviousTime = startTime;
            IsPlaying = montage != null;
            IsPaused = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            IsPaused = false;
        }

        public void Pause(bool paused)
        {
            IsPaused = paused;
        }

        public void Advance(float deltaTime)
        {
            if (!IsPlaying || IsPaused || Montage == null)
                return;

            PreviousTime = CurrentTime;
            CurrentTime += deltaTime * Montage.RateScale;

            float length = Montage.Length;
            if (length > 0f && CurrentTime > length)
            {
                CurrentTime = length;
                IsPlaying = false;
            }
        }

        public void SetTime(float time)
        {
            PreviousTime = CurrentTime;
            CurrentTime = Montage != null ? UnityEngine.Mathf.Clamp(time, 0f, Montage.Length) : 0f;
        }
    }
}
