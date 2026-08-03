using System;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime
{
    /// <summary>한 기능이 특정 알림 키에 추가하는 개수를 독립적으로 관리합니다.</summary>
    public sealed class NotificationDotHandle : IDisposable
    {
        private NotificationDotSystem owner;
        private readonly string key;
        private readonly int generation;
        private int count;

        internal NotificationDotHandle(NotificationDotSystem owner, string key, int generation, int initialCount)
        {
            this.owner = owner;
            this.key = key;
            this.generation = generation;
            SetCount(initialCount);
        }

        public string Key => key;
        public int Count => count;
        public bool IsDisposed => owner == null;

        public void SetCount(int value)
        {
            if (owner == null)
                return;

            value = Math.Max(0, value);
            if (count == value)
                return;

            int previous = count;
            count = value;
            owner.ChangeHandleCount(key, previous, value, generation);
        }

        public void Add(int amount)
        {
            if (amount == 0 || owner == null)
                return;

            long next = (long)count + amount;
            SetCount(next <= 0 ? 0 : next >= int.MaxValue ? int.MaxValue : (int)next);
        }

        public void Clear() => SetCount(0);

        public void Dispose()
        {
            if (owner == null)
                return;

            NotificationDotSystem currentOwner = owner;
            owner = null;
            currentOwner.ChangeHandleCount(key, count, 0, generation);
            count = 0;
        }
    }

    /// <summary>런타임에 등록한 알림 정의의 수명을 관리합니다.</summary>
    public sealed class NotificationDotRegistration : IDisposable
    {
        private NotificationDotSystem owner;
        private readonly string key;
        private readonly long id;
        private readonly int generation;

        internal NotificationDotRegistration(
            NotificationDotSystem owner,
            string key,
            long id,
            int generation)
        {
            this.owner = owner;
            this.key = key;
            this.id = id;
            this.generation = generation;
        }

        public string Key => key;
        public bool IsDisposed => owner == null;

        public void Dispose()
        {
            if (owner == null)
                return;

            NotificationDotSystem current = owner;
            owner = null;
            current.UnregisterDefinition(key, id, generation);
        }
    }}
