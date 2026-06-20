using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InMemoryItemDatabase : IItemCatalog
    {
        private readonly Dictionary<int, ItemCatalogEntry> entries = new();

        public IReadOnlyCollection<int> ItemIds => entries.Keys;

        public void Clear() => entries.Clear();

        public void Register(in ItemCatalogEntry entry)
        {
            if (entry.ItemId <= 0)
                return;

            entries[entry.ItemId] = entry;
        }

        public void Register(
            in ItemDefinition definition,
            string displayName = null,
            string description = null,
            string iconKey = null,
            string[] tags = null) =>
            Register(new ItemCatalogEntry(definition, displayName, description, iconKey, tags));

        public void RegisterRange(IEnumerable<ItemCatalogEntry> source)
        {
            if (source == null)
                return;

            foreach (ItemCatalogEntry entry in source)
                Register(entry);
        }

        public bool TryGetDefinition(int itemId, out ItemDefinition definition)
        {
            if (entries.TryGetValue(itemId, out ItemCatalogEntry entry))
            {
                definition = entry.Definition;
                return true;
            }

            definition = default;
            return false;
        }

        public bool TryGetEntry(int itemId, out ItemCatalogEntry entry) =>
            entries.TryGetValue(itemId, out entry);

        public void FindByTag(string tag, List<ItemCatalogEntry> results)
        {
            results.Clear();
            if (string.IsNullOrEmpty(tag))
                return;

            foreach (KeyValuePair<int, ItemCatalogEntry> pair in entries)
            {
                if (pair.Value.HasTag(tag))
                    results.Add(pair.Value);
            }
        }
    }
}
