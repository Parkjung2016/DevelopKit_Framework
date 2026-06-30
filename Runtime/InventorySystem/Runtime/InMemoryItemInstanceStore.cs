using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InMemoryItemInstanceStore : IItemInstanceStore
    {
        private readonly Dictionary<long, IItemInstanceData> entries = new();

        public bool TryGet<T>(long instanceId, out T data) where T : class, IItemInstanceData
        {
            data = null;
            if (instanceId <= 0 || !entries.TryGetValue(instanceId, out IItemInstanceData stored))
                return false;

            data = stored as T;
            return data != null;
        }

        public void Set<T>(long instanceId, T data) where T : class, IItemInstanceData
        {
            if (instanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            entries[instanceId] = data;
        }

        public bool Remove(long instanceId)
        {
            if (instanceId <= 0)
                return false;

            return entries.Remove(instanceId);
        }

        public bool Contains(long instanceId) => instanceId > 0 && entries.ContainsKey(instanceId);
    }
}
