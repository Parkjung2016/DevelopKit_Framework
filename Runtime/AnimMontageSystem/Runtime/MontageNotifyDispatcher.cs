using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>재생 시간의 변화를 기준으로 Notify와 NotifyState 콜백을 정확히 한 번씩 전달합니다.</summary>
    public sealed class MontageNotifyDispatcher
    {
        private readonly List<AnimNotifyStatePlacement> activeStates = new();
        private readonly List<AnimNotifyStatePlacement> beginBuffer = new();
        private readonly List<AnimNotifyStatePlacement> endBuffer = new();
        private readonly List<AnimNotifyStatePlacement> tickBuffer = new();
        private readonly List<AnimNotifyPlacement> notifyBuffer = new();
        private readonly Dictionary<AnimNotifyPlacement, float> lastNotifyTimes = new();

        /// <summary>Notify가 기본 처리까지 완료된 뒤 발생합니다. Handler가 처리한 Notify에는 호출되지 않습니다.</summary>
        public event Action<AnimNotify, AnimNotifyContext> OnNotify;


        /// <summary>현재 활성 상태와 중복 실행 기록을 모두 초기화합니다.</summary>
        public void Reset()
        {
            activeStates.Clear();
            lastNotifyTimes.Clear();
        }

        /// <summary>Montage가 중단될 때 실행 중인 모든 NotifyState에 OnEnd를 전달합니다.</summary>
        public void EndActiveStates(GameObject owner, Animator animator, AnimMontageSO montage, float montageTime, float deltaTime = 0f)
        {
            if (owner == null || activeStates.Count == 0)
            {
                Reset();
                return;
            }

            for (int i = activeStates.Count - 1; i >= 0; i--)
            {
                AnimNotifyStatePlacement placement = activeStates[i];
                AnimNotifyState state = placement?.NotifyState;
                if (state == null)
                    continue;

                float endTime = placement.EndTime > 0f ? placement.EndTime : montageTime;
                var context = new AnimNotifyContext(owner, animator, montage, endTime, deltaTime);
                state.OnEnd(context);
            }

            Reset();
        }

        /// <summary>이전 재생 시각부터 현재 시각 사이에 발생한 Notify 이벤트를 전달합니다.</summary>
        public void Dispatch(MontagePlaybackState playback, GameObject owner, Animator animator, IAnimNotifyHandler handler)
        {
            if (playback?.Montage == null || owner == null)
                return;

            AnimMontageSO montage = playback.Montage;
            float previousTime = playback.PreviousTime;
            float currentTime = playback.CurrentTime;
            float deltaTime = currentTime - previousTime;

            var context = new AnimNotifyContext(owner, animator, montage, currentTime, deltaTime);

            MontageEvaluator.CollectNotifyEvents(montage, previousTime, currentTime, notifyBuffer);
            for (int i = 0; i < notifyBuffer.Count; i++)
            {
                AnimNotifyPlacement placement = notifyBuffer[i];
                AnimNotify notify = placement.Notify;
                if (notify == null)
                    continue;

                if (lastNotifyTimes.TryGetValue(placement, out float lastTime)
                    && Mathf.Abs(lastTime - placement.Time) < 0.00001f)
                    continue;

                lastNotifyTimes[placement] = placement.Time;
                var notifyContext = new AnimNotifyContext(owner, animator, montage, placement.Time, deltaTime);
                if (handler != null && handler.TryHandle(notify, notifyContext))
                    continue;

                notify.OnNotify(notifyContext);
                OnNotify?.Invoke(notify, notifyContext);
            }

            MontageEvaluator.CollectNotifyStateTransitions(
                montage,
                previousTime,
                currentTime,
                beginBuffer,
                endBuffer,
                tickBuffer);

            for (int i = 0; i < endBuffer.Count; i++)
            {
                AnimNotifyStatePlacement placement = endBuffer[i];
                AnimNotifyState state = placement.NotifyState;
                var endContext = new AnimNotifyContext(owner, animator, montage, placement.EndTime, deltaTime);
                state?.OnEnd(endContext);
            }

            for (int i = 0; i < beginBuffer.Count; i++)
            {
                AnimNotifyStatePlacement placement = beginBuffer[i];
                AnimNotifyState state = placement.NotifyState;
                var beginContext = new AnimNotifyContext(owner, animator, montage, placement.StartTime, deltaTime);
                state?.OnBegin(beginContext);
            }

            for (int i = 0; i < tickBuffer.Count; i++)
            {
                AnimNotifyState state = tickBuffer[i].NotifyState;
                state?.OnTick(context, Mathf.Abs(deltaTime));
            }

            activeStates.Clear();
            activeStates.AddRange(tickBuffer);
        }

        /// <summary>수동 탐색 위치에 맞춰 활성 NotifyState만 복원하며 Begin, Tick 콜백은 실행하지 않습니다.</summary>
        public void ScrubTo(MontagePlaybackState playback, GameObject owner, Animator animator)
        {
            EndActiveStates(owner, animator, playback?.Montage, playback?.CurrentTime ?? 0f);
            activeStates.Clear();
            lastNotifyTimes.Clear();
            if (playback?.Montage == null)
                return;

            IReadOnlyList<AnimNotifyStatePlacement> states = playback.Montage.NotifyStates;
            for (int i = 0; i < states.Count; i++)
            {
                AnimNotifyStatePlacement placement = states[i];
                if (placement != null && placement.ContainsTime(playback.CurrentTime))
                    activeStates.Add(placement);
            }
        }
    }
}
