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

        public event Action<AnimNotify, AnimNotifyContext> OnNotify;

        public void Reset()
        {
            activeStates.Clear();
            lastNotifyTimes.Clear();
        }

        public void EndActiveStates(
            GameObject owner,
            Animator animator,
            IAnimationNotifyAsset animation,
            float animationTime,
            float deltaTime = 0f)
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

                float endTime = placement.EndTime > 0f ? placement.EndTime : animationTime;
                state.OnEnd(new AnimNotifyContext(owner, animator, animation, endTime, deltaTime));
            }

            Reset();
        }

        public void Dispatch(
            MontagePlaybackState playback,
            GameObject owner,
            Animator animator,
            IAnimNotifyHandler handler)
        {
            if (playback?.Montage == null)
                return;

            Dispatch(
                playback.Montage,
                playback.PreviousTime,
                playback.CurrentTime,
                owner,
                animator,
                handler);
        }

        /// <summary>Animation Sequence 또는 Montage의 지정 시간 구간을 평가합니다.</summary>
        public void Dispatch(
            IAnimationNotifyAsset animation,
            float previousTime,
            float currentTime,
            GameObject owner,
            Animator animator,
            IAnimNotifyHandler handler = null,
            float animationWeight = 1f)
        {
            if (animation == null || owner == null)
                return;

            float deltaTime = currentTime - previousTime;
            var context = new AnimNotifyContext(owner, animator, animation, currentTime, deltaTime);

            MontageEvaluator.CollectNotifyEvents(animation, previousTime, currentTime, notifyBuffer);
            for (int i = 0; i < notifyBuffer.Count; i++)
            {
                AnimNotifyPlacement placement = notifyBuffer[i];
                AnimNotify notify = placement.Notify;
                if (notify == null || animationWeight < placement.TriggerWeightThreshold)
                    continue;

                if (lastNotifyTimes.TryGetValue(placement, out float lastTime)
                    && Mathf.Abs(lastTime - placement.Time) < 0.00001f)
                {
                    continue;
                }

                lastNotifyTimes[placement] = placement.Time;
                var notifyContext = new AnimNotifyContext(owner, animator, animation, placement.Time, deltaTime);
                if (handler != null && handler.TryHandle(notify, notifyContext))
                    continue;

                notify.OnNotify(notifyContext);
                OnNotify?.Invoke(notify, notifyContext);
            }

            MontageEvaluator.CollectNotifyStateTransitions(
                animation,
                previousTime,
                currentTime,
                beginBuffer,
                endBuffer,
                tickBuffer);

            for (int i = 0; i < endBuffer.Count; i++)
            {
                AnimNotifyStatePlacement placement = endBuffer[i];
                placement.NotifyState?.OnEnd(
                    new AnimNotifyContext(owner, animator, animation, placement.EndTime, deltaTime));
            }

            for (int i = 0; i < beginBuffer.Count; i++)
            {
                AnimNotifyStatePlacement placement = beginBuffer[i];
                placement.NotifyState?.OnBegin(
                    new AnimNotifyContext(owner, animator, animation, placement.StartTime, deltaTime));
            }

            for (int i = 0; i < tickBuffer.Count; i++)
                tickBuffer[i].NotifyState?.OnTick(context, Mathf.Abs(deltaTime));

            activeStates.Clear();
            activeStates.AddRange(tickBuffer);
        }

        public void ScrubTo(MontagePlaybackState playback, GameObject owner, Animator animator)
        {
            EndActiveStates(owner, animator, playback?.Montage, playback?.CurrentTime ?? 0f);
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