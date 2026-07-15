using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    /// <summary>같은 가중치 목록에서 반복 선택할 때 합계를 재계산하지 않는 선택 테이블입니다.</summary>
    public sealed class WeightedTable<T>
    {
        private T[] items = Array.Empty<T>();
        private double[] cumulativeWeights = Array.Empty<double>();
        private double totalWeight;

        public int Count => items.Length;

        public WeightedTable(IReadOnlyList<T> source, Func<T, double> getWeight)
        {
            Rebuild(source, getWeight);
        }

        public void Rebuild(IReadOnlyList<T> source, Func<T, double> getWeight)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (getWeight == null)
                throw new ArgumentNullException(nameof(getWeight));

            var validItems = new List<T>(source.Count);
            var weights = new List<double>(source.Count);
            double total = 0d;

            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                double weight = getWeight(item);
                if (!(weight > 0d) || double.IsNaN(weight) || double.IsInfinity(weight))
                    continue;

                total += weight;
                validItems.Add(item);
                weights.Add(total);
            }

            items = validItems.ToArray();
            cumulativeWeights = weights.ToArray();
            totalWeight = total;
        }

        public bool TryPick(out T item, IRandomSource random = null)
        {
            if (items.Length == 0 || !(totalWeight > 0d))
            {
                item = default;
                return false;
            }

            random ??= RandomProvider.Shared;
            double target = random.NextDouble() * totalWeight;
            int low = 0;
            int high = cumulativeWeights.Length - 1;
            while (low < high)
            {
                int middle = low + ((high - low) >> 1);
                if (target < cumulativeWeights[middle])
                    high = middle;
                else
                    low = middle + 1;
            }

            item = items[low];
            return true;
        }

        public T Pick(IRandomSource random = null) =>
            TryPick(out T item, random)
                ? item
                : throw new InvalidOperationException("The weighted table has no selectable items.");
    }
}
