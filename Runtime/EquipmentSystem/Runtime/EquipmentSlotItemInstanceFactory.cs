using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>
    /// 장비 아이템을 <see cref="IEquipmentItemProfileSource"/> SlotCategory(Weapon, Head …)별 Factory로 라우팅합니다.
    /// </summary>
    public sealed class EquipmentSlotItemInstanceFactory : IItemInstanceFactory
    {
        private readonly IEquipmentItemProfileSource profileSource;
        private readonly Dictionary<string, IItemInstanceFactory> factoriesByCategory =
            new(StringComparer.OrdinalIgnoreCase);
        private IItemInstanceFactory fallback;

        public EquipmentSlotItemInstanceFactory(IEquipmentItemProfileSource profileSource)
        {
            this.profileSource = profileSource ?? throw new ArgumentNullException(nameof(profileSource));
        }

        public EquipmentSlotItemInstanceFactory Set(string slotCategory, IItemInstanceFactory factory)
        {
            if (string.IsNullOrEmpty(slotCategory))
                throw new ArgumentException("Slot category is required.", nameof(slotCategory));

            factoriesByCategory[slotCategory] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public EquipmentSlotItemInstanceFactory Set(string slotCategory, Func<int, IItemInstanceData> create) =>
            Set(slotCategory, ItemInstanceFactories.Delegate(create));

        public EquipmentSlotItemInstanceFactory Set<T>(string slotCategory) where T : class, IItemInstanceData, new() =>
            Set(slotCategory, ItemInstanceFactories.Create<T>());

        public EquipmentSlotItemInstanceFactory Set(string slotCategory, Func<IItemInstanceData> create) =>
            Set(slotCategory, ItemInstanceFactories.Create(create));

        public EquipmentSlotItemInstanceFactory SetFallback(IItemInstanceFactory factory)
        {
            fallback = factory;
            return this;
        }

        public EquipmentSlotItemInstanceFactory SetFallback(Func<int, IItemInstanceData> create) =>
            SetFallback(ItemInstanceFactories.Delegate(create));

        public EquipmentSlotItemInstanceFactory SetFallback<T>() where T : class, IItemInstanceData, new() =>
            SetFallback(ItemInstanceFactories.Create<T>());

        public EquipmentSlotItemInstanceFactory SetFallback() => SetFallback<EmptyItemInstanceData>();

        public bool TryCreate(int itemId, out IItemInstanceData data)
        {
            data = null;
            if (!ItemCatalog.TryGetDefinition(itemId, out ItemDefinition definition))
                return fallback?.TryCreate(itemId, out data) ?? false;

            if (definition.ItemType != ItemType.Equipment)
                return false;

            if (profileSource.TryGetSlotCategory(itemId, definition, out string slotCategory)
                && !string.IsNullOrEmpty(slotCategory)
                && factoriesByCategory.TryGetValue(slotCategory, out IItemInstanceFactory factory)
                && factory.TryCreate(itemId, out data))
                return true;

            return fallback?.TryCreate(itemId, out data) ?? false;
        }
    }
}
