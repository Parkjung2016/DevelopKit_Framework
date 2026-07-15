using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>
    /// 현재 몽타주의 재생 시간과 재생 상태를 관리합니다.
    /// </summary>
    public sealed class MontagePlaybackState
    {
        public AnimMontageSO Montage { get; private set; }
        public float CurrentTime { get; private set; }
        public float PreviousTime { get; private set; }
        public float Duration { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }

        public float NormalizedTime => Duration > 0f
            ? Mathf.Clamp01(CurrentTime / Duration)
            : 0f;

        public void Begin(AnimMontageSO montage, float startTime)
        {
            Montage = montage;
            Duration = montage != null ? montage.Length : 0f;
            CurrentTime = Mathf.Clamp(startTime, 0f, Duration);
            PreviousTime = CurrentTime;
            IsPlaying = montage != null;
            IsPaused = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            IsPaused = false;
        }

        public void Pause(bool paused) => IsPaused = paused;

        public void Advance(float deltaTime)
        {
            if (!IsPlaying || IsPaused || Montage == null)
                return;

            PreviousTime = CurrentTime;
            CurrentTime += deltaTime * Montage.RateScale;
            if (Duration > 0f && CurrentTime < Duration)
                return;

            CurrentTime = Duration;
            IsPlaying = false;
        }

        public void SetTime(float time)
        {
            PreviousTime = CurrentTime;
            CurrentTime = Montage != null
                ? Mathf.Clamp(time, 0f, Duration)
                : 0f;
        }
    }
}