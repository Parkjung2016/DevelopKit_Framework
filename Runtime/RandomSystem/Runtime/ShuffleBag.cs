using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    /// <summary>등록한 항목을 한 주기씩 섞어서 뽑아 단기 쏠림을 줄입니다.</summary>
    public sealed class ShuffleBag<T>
    {
        private readonly List<T> entries = new();
        private readonly List<int> order = new();
        private readonly IRandomSource random;
        private readonly IEqualityComparer<T> comparer;

        private int nextIndex;
        private bool hasLast;
        private T last;

        public ShuffleBag(IRandomSource random = null, IEqualityComparer<T> comparer = null)
        {
            this.random = random ?? RandomProvider.Shared;
            this.comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public int Count => entries.Count;

        public void Add(T item, int copies = 1)
        {
            if (copies <= 0)
                throw new ArgumentOutOfRangeException(nameof(copies));

            for (int i = 0; i < copies; i++)
                entries.Add(item);

            Refill();
        }

        public void Clear()
        {
            entries.Clear();
            order.Clear();
            nextIndex = 0;
            hasLast = false;
            last = default;
        }

        public T Next()
        {
            if (entries.Count == 0)
                throw new InvalidOperationException("The shuffle bag is empty.");
            if (nextIndex >= order.Count)
                Refill();

            T item = entries[order[nextIndex++]];
            last = item;
            hasLast = true;
            return item;
        }

        private void Refill()
        {
            order.Clear();
            if (order.Capacity < entries.Count)
                order.Capacity = entries.Count;

            for (int i = 0; i < entries.Count; i++)
                order.Add(i);

            RandomPick.Shuffle(order, random);
            nextIndex = 0;

            if (!hasLast || order.Count < 2 || !comparer.Equals(entries[order[0]], last))
                return;

            for (int i = 1; i < order.Count; i++)
            {
                if (comparer.Equals(entries[order[i]], last))
                    continue;

                (order[0], order[i]) = (order[i], order[0]);
                break;
            }
        }
    }
}
