using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public static class EquipmentTagUtility
    {
        public static bool TryGetCategoryFromTags(string[] tags, string tagPrefix, out string slotCategory)
        {
            slotCategory = null;
            if (tags == null || tags.Length == 0 || string.IsNullOrEmpty(tagPrefix))
                return false;

            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrEmpty(tag) || !tag.StartsWith(tagPrefix, StringComparison.Ordinal))
                    continue;

                slotCategory = tag.Substring(tagPrefix.Length);
                return !string.IsNullOrEmpty(slotCategory);
            }

            return false;
        }
    }

    public sealed class CatalogTagEquipmentProfileSource : IEquipmentItemProfileSource
    {
        private readonly IItemCatalog catalog;
        private readonly string tagPrefix;
        private readonly Dictionary<int, string> categoriesByItemId = new();

        public CatalogTagEquipmentProfileSource(IItemCatalog catalog, string tagPrefix = "equip.")
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.tagPrefix = string.IsNullOrEmpty(tagPrefix) ? "equip." : tagPrefix;
        }

        public bool TryGetSlotCategory(int itemId, in ItemDefinition definition, out string slotCategory)
        {
            if (categoriesByItemId.TryGetValue(itemId, out slotCategory))
                return !string.IsNullOrEmpty(slotCategory);

            if (!catalog.TryGetEntry(itemId, out ItemCatalogEntry entry) ||
                !EquipmentTagUtility.TryGetCategoryFromTags(entry.Tags, tagPrefix, out slotCategory))
            {
                categoriesByItemId[itemId] = null;
                return false;
            }

            categoriesByItemId[itemId] = slotCategory;
            return true;
        }

        public void ClearCache() => categoriesByItemId.Clear();
    }
}
