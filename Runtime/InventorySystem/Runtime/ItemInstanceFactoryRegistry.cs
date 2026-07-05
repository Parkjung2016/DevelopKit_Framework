using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary><paramref name="itemId"/>별 <see cref="IItemInstanceFactory"/>를 등록합니다.</summary>
    public sealed class ItemInstanceFactoryRegistry : IItemInstanceFactory
    {
        private readonly Dictionary<int, IItemInstanceFactory> factoriesByItemId = new();
        private IItemInstanceFactory fallback;

        public void Register(int itemId, IItemInstanceFactory factory)
        {
            if (itemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(itemId));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            factoriesByItemId[itemId] = factory;
        }

        public void RegisterRange(IReadOnlyDictionary<int, IItemInstanceFactory> factories)
        {
            if (factories == null)
                throw new ArgumentNullException(nameof(factories));

            foreach (KeyValuePair<int, IItemInstanceFactory> pair in factories)
                Register(pair.Key, pair.Value);
        }

        public void SetFallback(IItemInstanceFactory factory) => fallback = factory;

        public bool TryCreate(int itemId, out IItemInstanceData data)
        {
            if (factoriesByItemId.TryGetValue(itemId, out IItemInstanceFactory factory)
                && factory.TryCreate(itemId, out data))
                return true;

            if (fallback != null && fallback.TryCreate(itemId, out data))
                return true;

            data = null;
            return false;
        }
    }
}
