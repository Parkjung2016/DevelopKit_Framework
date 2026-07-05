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

        public event Action<AnimNotifySO, AnimNotifyContext> NotifyFired;

        public void Reset()
        {
            activeStates.Clear();
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
                AnimNotifySO notify = placement.Notify;
                if (notify == null)
                    continue;

                if (handler != null && handler.TryHandle(notify, context))
                    continue;

                notify.OnNotify(context);
                NotifyFired?.Invoke(notify, context);
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
                AnimNotifyStateSO state = endBuffer[i].NotifyState;
                state?.OnEnd(context);
            }

            for (int i = 0; i < beginBuffer.Count; i++)
            {
                AnimNotifyStateSO state = beginBuffer[i].NotifyState;
                state?.OnBegin(context);
            }

            for (int i = 0; i < tickBuffer.Count; i++)
            {
                AnimNotifyStateSO state = tickBuffer[i].NotifyState;
                state?.OnTick(context, Mathf.Abs(deltaTime));
            }

            activeStates.Clear();
            activeStates.AddRange(tickBuffer);
        }

        public void ScrubTo(MontagePlaybackState playback, GameObject owner, Animator animator)
        {
            activeStates.Clear();
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
