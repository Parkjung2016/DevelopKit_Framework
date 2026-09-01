using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>Animation Sequence와 Montage의 Notify 타임라인을 평가합니다.</summary>
    public static class MontageEvaluator
    {
        public static void CollectNotifyEvents(
            IAnimationNotifyAsset animation,
            float previousTime,
            float currentTime,
            List<AnimNotifyPlacement> results)
        {
            results?.Clear();
            if (animation == null || results == null)
                return;

            IReadOnlyList<AnimNotifyPlacement> notifies = animation.Notifies;
            for (int i = 0; i < notifies.Count; i++)
            {
                AnimNotifyPlacement placement = notifies[i];
                if (placement?.Notify == null)
                    continue;

                if (Crossed(previousTime, currentTime, placement.Time))
                    results.Add(placement);
            }
        }

        public static void CollectNotifyStateTransitions(
            IAnimationNotifyAsset animation,
            float previousTime,
            float currentTime,
            List<AnimNotifyStatePlacement> beginStates,
            List<AnimNotifyStatePlacement> endStates,
            List<AnimNotifyStatePlacement> activeStates)
        {
            beginStates?.Clear();
            endStates?.Clear();
            activeStates?.Clear();
            if (animation == null)
                return;

            IReadOnlyList<AnimNotifyStatePlacement> states = animation.NotifyStates;
            for (int i = 0; i < states.Count; i++)
            {
                AnimNotifyStatePlacement placement = states[i];
                if (placement?.NotifyState == null)
                    continue;

                bool wasActive = placement.ContainsTime(previousTime);
                bool isActive = placement.ContainsTime(currentTime);
                bool sameTime = Math.Abs(previousTime - currentTime) < 0.00001f;
                if ((sameTime && isActive) || (!wasActive && isActive))
                    beginStates?.Add(placement);
                else if (wasActive && !isActive)
                    endStates?.Add(placement);

                if (isActive)
                    activeStates?.Add(placement);
            }
        }

        private static bool Crossed(float previousTime, float currentTime, float markerTime)
        {
            if (Math.Abs(previousTime - currentTime) < 0.00001f)
                return Math.Abs(markerTime - currentTime) < 0.00001f;

            if (previousTime <= currentTime)
                return markerTime > previousTime && markerTime <= currentTime;

            return markerTime <= previousTime && markerTime > currentTime;
        }
    }
}