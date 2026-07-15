using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    /// <summary>
    /// 반복해서 사용하는 참조 타입 인스턴스를 재사용합니다.
    /// 스레드 안전하지 않으며 생성된 스레드에서 사용해야 합니다.
    /// </summary>
    public sealed class Pool<T> where T : class
    {
        private readonly Stack<T> inactiveItems;
        private readonly HashSet<T> inactiveSet;
        private readonly HashSet<T> knownItems;
        private readonly Func<T> create;
        private readonly Action<T> onRent;
        private readonly Action<T> onReturn;
        private readonly Action<T> onDestroy;

        private int countAll;

        public int CountAll => countAll;
        public int CountInactive => inactiveItems.Count;
        public int CountActive => Math.Max(0, countAll - inactiveItems.Count);
        public int MaxSize { get; }

        public Pool(
            Func<T> create,
            Action<T> onRent = null,
            Action<T> onReturn = null,
            Action<T> onDestroy = null,
            int initialCapacity = 0,
            int maxSize = 128,
            bool collectionCheck = true)
        {
            this.create = create ?? throw new ArgumentNullException(nameof(create));

            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            if (maxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            if (initialCapacity > maxSize)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            this.onRent = onRent;
            this.onReturn = onReturn;
            this.onDestroy = onDestroy;
            MaxSize = maxSize;
            inactiveItems = new Stack<T>(initialCapacity);

            if (collectionCheck)
            {
                inactiveSet = new HashSet<T>(ReferenceComparer<T>.Instance);
                knownItems = new HashSet<T>(ReferenceComparer<T>.Instance);
            }

            Prewarm(initialCapacity);
        }

        public T Rent()
        {
            T item;
            if (inactiveItems.Count > 0)
            {
                item = inactiveItems.Pop();
                inactiveSet?.Remove(item);
            }
            else
            {
                item = CreateItem();
            }

            onRent?.Invoke(item);
            return item;
        }

        public PoolLease<T> Rent(out T item)
        {
            item = Rent();
            return new PoolLease<T>(this, item);
        }

        public void Return(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (knownItems != null && !knownItems.Contains(item))
                throw new InvalidOperationException("The item was not created by this pool.");
            if (inactiveSet != null && inactiveSet.Contains(item))
                throw new InvalidOperationException("The item has already been returned to this pool.");

            onReturn?.Invoke(item);

            if (inactiveItems.Count >= MaxSize)
            {
                knownItems?.Remove(item);
                countAll = Math.Max(0, countAll - 1);
                onDestroy?.Invoke(item);
                return;
            }

            inactiveItems.Push(item);
            inactiveSet?.Add(item);
        }

        public void Prewarm(int count)
        {
            if (count < 0 || count > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(count));

            while (inactiveItems.Count < count)
            {
                T item = CreateItem();
                onReturn?.Invoke(item);
                inactiveItems.Push(item);
                inactiveSet?.Add(item);
            }
        }

        public void Clear()
        {
            while (inactiveItems.Count > 0)
            {
                T item = inactiveItems.Pop();
                inactiveSet?.Remove(item);
                knownItems?.Remove(item);
                countAll = Math.Max(0, countAll - 1);
                onDestroy?.Invoke(item);
            }
        }

        internal void Forget(T item)
        {
            if (item == null || knownItems == null || !knownItems.Remove(item))
                return;

            if (inactiveSet.Remove(item))
            {
                var temporary = new Stack<T>(inactiveItems.Count);
                while (inactiveItems.Count > 0)
                {
                    T current = inactiveItems.Pop();
                    if (!ReferenceEquals(current, item))
                        temporary.Push(current);
                }

                while (temporary.Count > 0)
                    inactiveItems.Push(temporary.Pop());
            }

            countAll = Math.Max(0, countAll - 1);
        }
        private T CreateItem()
        {
            T item = create() ?? throw new InvalidOperationException("Pool create callback returned null.");
            countAll++;
            knownItems?.Add(item);
            return item;
        }
    }
}