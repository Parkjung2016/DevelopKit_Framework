using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary><paramref name="itemId"/>별 <see cref="IItemInstanceFactory"/> 레지스트리입니다.</summary>
    public sealed class ItemInstanceFactoryRegistry : IItemInstanceFactory
    {
        private readonly Dictionary<int, IItemInstanceFactory> factoriesByItemId = new();
        private IItemInstanceFactory fallback;

        public ItemInstanceFactoryRegistry Register(int itemId, IItemInstanceFactory factory)
        {
            if (itemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(itemId));

            factoriesByItemId[itemId] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public ItemInstanceFactoryRegistry Register(int itemId, Func<int, IItemInstanceData> create) =>
            Register(itemId, ItemInstanceFactories.Delegate(create));

        public ItemInstanceFactoryRegistry Register<T>(int itemId) where T : class, IItemInstanceData, new() =>
            Register(itemId, ItemInstanceFactories.Create<T>());

        public ItemInstanceFactoryRegistry Register(int itemId, Func<IItemInstanceData> create) =>
            Register(itemId, ItemInstanceFactories.Create(create));

        public void RegisterRange(IReadOnlyDictionary<int, IItemInstanceFactory> factories)
        {
            if (factories == null)
                throw new ArgumentNullException(nameof(factories));

            foreach (KeyValuePair<int, IItemInstanceFactory> pair in factories)
                Register(pair.Key, pair.Value);
        }

        public ItemInstanceFactoryRegistry SetFallback(IItemInstanceFactory factory)
        {
            fallback = factory;
            return this;
        }

        public ItemInstanceFactoryRegistry SetFallback(Func<int, IItemInstanceData> create) =>
            SetFallback(ItemInstanceFactories.Delegate(create));

        public ItemInstanceFactoryRegistry SetFallback<T>() where T : class, IItemInstanceData, new() =>
            SetFallback(ItemInstanceFactories.Create<T>());

        public ItemInstanceFactoryRegistry SetFallback(Func<IItemInstanceData> create) =>
            SetFallback(ItemInstanceFactories.Create(create));

        public ItemInstanceFactoryRegistry SetFallback() => SetFallback<EmptyItemInstanceData>();

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
