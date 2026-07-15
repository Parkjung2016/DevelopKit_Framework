using System;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    /// <summary>using 블록이 끝날 때 대여한 인스턴스를 자동으로 반환합니다.</summary>
    public struct PoolLease<T> : IDisposable where T : class
    {
        private Pool<T> pool;

        public T Value { get; }

        internal PoolLease(Pool<T> pool, T value)
        {
            this.pool = pool;
            Value = value;
        }

        public void Dispose()
        {
            Pool<T> owner = pool;
            pool = null;
            if (owner != null && Value != null)
                owner.Return(Value);
        }
    }
}