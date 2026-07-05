using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary><see cref="ItemDefinition.ItemType"/>별 <see cref="IItemInstanceFactory"/> 라우터입니다.</summary>
    public sealed class ItemTypeItemInstanceFactory : IItemInstanceFactory
    {
        private readonly Dictionary<ItemType, IItemInstanceFactory> factoriesByType = new();
        private IItemInstanceFactory fallback;

        public ItemTypeItemInstanceFactory Set(ItemType itemType, IItemInstanceFactory factory)
        {
            factoriesByType[itemType] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public ItemTypeItemInstanceFactory Set(ItemType itemType, Func<int, IItemInstanceData> create) =>
            Set(itemType, ItemInstanceFactories.Delegate(create));

        public ItemTypeItemInstanceFactory Set<T>(ItemType itemType) where T : class, IItemInstanceData, new() =>
            Set(itemType, ItemInstanceFactories.Create<T>());

        public ItemTypeItemInstanceFactory Set(ItemType itemType, Func<IItemInstanceData> create) =>
            Set(itemType, ItemInstanceFactories.Create(create));

        public ItemTypeItemInstanceFactory SetFallback(IItemInstanceFactory factory)
        {
            fallback = factory;
            return this;
        }

        public ItemTypeItemInstanceFactory SetFallback(Func<int, IItemInstanceData> create) =>
            SetFallback(ItemInstanceFactories.Delegate(create));

        public ItemTypeItemInstanceFactory SetFallback<T>() where T : class, IItemInstanceData, new() =>
            SetFallback(ItemInstanceFactories.Create<T>());

        public ItemTypeItemInstanceFactory SetFallback(Func<IItemInstanceData> create) =>
            SetFallback(ItemInstanceFactories.Create(create));

        public ItemTypeItemInstanceFactory SetFallback() => SetFallback<EmptyItemInstanceData>();

        public bool TryCreate(int itemId, out IItemInstanceData data)
        {
            data = null;
            if (!ItemCatalog.TryGetDefinition(itemId, out ItemDefinition definition))
                return fallback?.TryCreate(itemId, out data) ?? false;

            if (factoriesByType.TryGetValue(definition.ItemType, out IItemInstanceFactory factory)
                && factory.TryCreate(itemId, out data))
                return true;

            return fallback?.TryCreate(itemId, out data) ?? false;
        }
    }
}
