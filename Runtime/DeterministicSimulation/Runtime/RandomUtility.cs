using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public static class RandomUtility
    {
        public static bool TryPickWeightedIndex(
            IReadOnlyList<int> candidateIndices,
            Func<int, float> getWeight,
            IRandomSource random,
            out int pickedIndex)
        {
            pickedIndex = -1;
            if (candidateIndices == null || candidateIndices.Count == 0 || getWeight == null || random == null)
                return false;

            float totalWeight = 0f;
            for (int i = 0; i < candidateIndices.Count; i++)
            {
                float weight = getWeight(candidateIndices[i]);
                if (weight > 0f)
                    totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return false;

            float roll = (float)(random.NextDouble() * totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < candidateIndices.Count; i++)
            {
                int entryIndex = candidateIndices[i];
                float weight = getWeight(entryIndex);
                if (weight <= 0f)
                    continue;

                cumulative += weight;
                if (roll <= cumulative)
                {
                    pickedIndex = entryIndex;
                    return true;
                }
            }

            for (int i = candidateIndices.Count - 1; i >= 0; i--)
            {
                int entryIndex = candidateIndices[i];
                if (getWeight(entryIndex) > 0f)
                {
                    pickedIndex = entryIndex;
                    return true;
                }
            }

            return false;
        }
    }
}
