using System;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>
    /// Montage 재생을 제어하고 현재 상태를 조회하는 공통 인터페이스입니다.
    /// </summary>
    public interface IAnimMontagePlayer
    {
        AnimMontageSO CurrentMontage { get; }
        float CurrentTime { get; }
        float Duration { get; }
        float NormalizedTime { get; }
        bool IsPlaying { get; }
        bool IsPaused { get; }

        event Action<AnimNotify, AnimNotifyContext> OnNotify;
        event Action<MontagePlaybackEventContext> OnPlaybackEvent;
        event Action<MontagePlaybackEventContext> OnPlay;
        event Action<MontagePlaybackEventContext> OnComplete;
        event Action<MontagePlaybackEventContext> OnStop;
        event Action<MontagePlaybackEventContext> OnInterrupted;

        void Play(AnimMontageSO montage, float startTime = 0f);
        void Stop();
        void Pause(bool paused = true);
        void SetTime(float montageTime);
    }
}
