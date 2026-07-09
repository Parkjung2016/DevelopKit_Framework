using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public static class MontageEvaluator
    {
        public static void CollectNotifyEvents(
            AnimMontageSO montage,
            float previousTime,
            float currentTime,
            List<AnimNotifyPlacement> results)
        {
            results?.Clear();
            if (montage == null || results == null)
                return;

            IReadOnlyList<AnimNotifyPlacement> notifies = montage.Notifies;
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
            AnimMontageSO montage,
            float previousTime,
            float currentTime,
            List<AnimNotifyStatePlacement> beginStates,
            List<AnimNotifyStatePlacement> endStates,
            List<AnimNotifyStatePlacement> activeStates)
        {
            beginStates?.Clear();
            endStates?.Clear();
            activeStates?.Clear();
            if (montage == null)
                return;

            IReadOnlyList<AnimNotifyStatePlacement> states = montage.NotifyStates;
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
