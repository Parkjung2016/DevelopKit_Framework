using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public sealed class MontageNotifyDispatcher
    {
        private readonly List<AnimNotifyStatePlacement> activeStates = new();
        private readonly List<AnimNotifyStatePlacement> beginBuffer = new();
        private readonly List<AnimNotifyStatePlacement> endBuffer = new();
        private readonly List<AnimNotifyStatePlacement> tickBuffer = new();
        private readonly List<AnimNotifyPlacement> notifyBuffer = new();
        private readonly Dictionary<AnimNotifyPlacement, float> lastNotifyTimes = new();

        public event Action<AnimNotify, AnimNotifyContext> OnNotify;

        // 이전 API와의 호환을 위해 유지합니다.
        public event Action<AnimNotify, AnimNotifyContext> NotifyFired
        {
            add => OnNotify += value;
            remove => OnNotify -= value;
        }

        public void Reset()
        {
            activeStates.Clear();
            lastNotifyTimes.Clear();
        }

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
