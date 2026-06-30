using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public sealed class EquipmentSlotRule : ISlotRule
    {
        private readonly string[] slotCategories;
        private readonly IEquipmentItemProfileSource profileSource;
        private readonly ItemType equipmentItemType;

        public EquipmentSlotRule(
            string[] slotCategories,
            IEquipmentItemProfileSource profileSource,
            ItemType equipmentItemType)
        {
            if (slotCategories == null || slotCategories.Length == 0)
                throw new ArgumentException("At least one slot category entry is required.", nameof(slotCategories));

            this.slotCategories = slotCategories;
            this.profileSource = profileSource ?? throw new ArgumentNullException(nameof(profileSource));
            this.equipmentItemType = equipmentItemType;
        }

        public bool CanAccept(int slotIndex, in ItemDefinition definition)
        {
            if (definition.ItemType != equipmentItemType)
                return false;

            if (slotIndex < 0 || slotIndex >= slotCategories.Length)
                return false;

            if (!profileSource.TryGetSlotCategory(definition.ItemId, definition, out string itemCategory))
                return false;

            string requiredCategory = slotCategories[slotIndex];
            if (string.IsNullOrEmpty(requiredCategory))
                return true;

            return string.Equals(requiredCategory, itemCategory, StringComparison.Ordinal);
        }
    }
}
