using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class ItemTypeSlotRule : ISlotRule
    {
        private readonly HashSet<ItemType> allowedTypes;

        public ItemTypeSlotRule(params ItemType[] allowedTypes)
        {
            if (allowedTypes == null || allowedTypes.Length == 0)
                throw new ArgumentException("At least one item type is required.", nameof(allowedTypes));

            this.allowedTypes = new HashSet<ItemType>(allowedTypes);
        }

        public bool CanAccept(int slotIndex, in ItemDefinition definition) =>
            allowedTypes.Contains(definition.ItemType);
    }
}
