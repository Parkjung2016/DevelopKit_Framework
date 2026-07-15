using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    public static class RandomPick
    {
        public static T Pick<T>(IReadOnlyList<T> items, IRandomSource random = null)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (items.Count == 0)
                throw new InvalidOperationException("Cannot pick from an empty collection.");

            random ??= RandomProvider.Shared;
            return items[random.NextInt(0, items.Count)];
        }

        public static void Shuffle<T>(IList<T> items, IRandomSource random = null)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            random ??= RandomProvider.Shared;
            for (int i = items.Count - 1; i > 0; i--)
            {
                int other = random.NextInt(0, i + 1);
                if (other == i)
                    continue;

                (items[i], items[other]) = (items[other], items[i]);
            }
        }

        public static bool TryWeightedIndex<T>(
            T[] items,
            IReadOnlyList<int> candidateIndices,
            Func<T, double> getWeight,
            IRandomSource random,
            out int pickedIndex)
        {
            pickedIndex = -1;
            if (items == null || items.Length == 0 || getWeight == null)
                return false;

            random ??= RandomProvider.Shared;
            int count = candidateIndices?.Count ?? items.Length;
            double total = 0d;
            for (int i = 0; i < count; i++)
            {
                int index = candidateIndices == null ? i : candidateIndices[i];
                if ((uint)index >= (uint)items.Length)
                    throw new ArgumentOutOfRangeException(nameof(candidateIndices));

                double weight = getWeight(items[index]);
                if (weight > 0d && !double.IsNaN(weight))
                    total += weight;
            }

            if (!(total > 0d) || double.IsInfinity(total))
                return false;

            double target = random.NextDouble() * total;
            double accumulated = 0d;
            for (int i = 0; i < count; i++)
            {
                int index = candidateIndices == null ? i : candidateIndices[i];
                double weight = getWeight(items[index]);
                if (!(weight > 0d) || double.IsNaN(weight))
                    continue;

                pickedIndex = index;
                accumulated += weight;
                if (target < accumulated)
                    return true;
            }

            return pickedIndex >= 0;
        }
    }
}
