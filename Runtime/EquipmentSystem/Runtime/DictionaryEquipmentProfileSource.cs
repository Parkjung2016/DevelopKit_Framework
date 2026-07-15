using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public sealed class DictionaryEquipmentProfileSource : IEquipmentItemProfileSource
    {
        private readonly Dictionary<int, string> categoriesByItemId;

        public DictionaryEquipmentProfileSource(Dictionary<int, string> categoriesByItemId) =>
            this.categoriesByItemId = categoriesByItemId == null
                ? throw new ArgumentNullException(nameof(categoriesByItemId))
                : new Dictionary<int, string>(categoriesByItemId);

        public bool TryGetSlotCategory(int itemId, in ItemDefinition definition, out string slotCategory)
        {
            if (categoriesByItemId.TryGetValue(itemId, out slotCategory))
                return !string.IsNullOrEmpty(slotCategory);

            slotCategory = null;
            return false;
        }
    }
}
