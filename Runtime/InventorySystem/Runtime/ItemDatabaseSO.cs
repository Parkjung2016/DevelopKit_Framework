using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_ItemDatabase", menuName = "PJDev/InventorySystem/ItemDatabase")]
    public class ItemDatabaseSO : ScriptableObject, IItemCatalog
    {
        [field: SerializeField] public ItemDefinitionSO[] Items { get; set; } = System.Array.Empty<ItemDefinitionSO>();

        private readonly Dictionary<int, ItemDefinition> definitionCache = new();
        private readonly Dictionary<int, ItemDefinitionSO> itemCache = new();
        private readonly Dictionary<int, ItemCatalogEntry> entryCache = new();

        public IReadOnlyCollection<int> ItemIds => itemCache.Keys;

        private void OnEnable() => RebuildCache();

        private void OnValidate() => RebuildCache();

        public void RebuildCache()
        {
            definitionCache.Clear();
            itemCache.Clear();
            entryCache.Clear();

            if (Items == null)
                return;

            for (int i = 0; i < Items.Length; i++)
            {
                ItemDefinitionSO item = Items[i];
                if (item == null || item.ItemId <= 0)
                    continue;

                if (itemCache.ContainsKey(item.ItemId))
                {
                    CDebug.LogWarning($"ItemDatabaseSO : duplicate item id {item.ItemId} in {name}.");
                    continue;
                }

                itemCache.Add(item.ItemId, item);
                definitionCache.Add(item.ItemId, item.ToDefinition());
                entryCache.Add(item.ItemId, CreateEntry(item));
            }
        }

        public bool TryGetDefinition(int itemId, out ItemDefinition definition) =>
            definitionCache.TryGetValue(itemId, out definition);

        public bool TryGetEntry(int itemId, out ItemCatalogEntry entry) =>
            entryCache.TryGetValue(itemId, out entry);

        public bool TryGetItem(int itemId, out ItemDefinitionSO item) =>
            itemCache.TryGetValue(itemId, out item);

        public void FindByTag(string tag, List<ItemCatalogEntry> results)
        {
            results.Clear();
            if (string.IsNullOrEmpty(tag) || Items == null)
                return;

            for (int i = 0; i < Items.Length; i++)
            {
                ItemDefinitionSO item = Items[i];
                if (item != null && item.HasTag(tag) && entryCache.TryGetValue(item.ItemId, out ItemCatalogEntry entry))
                    results.Add(entry);
            }
        }

        private static ItemCatalogEntry CreateEntry(ItemDefinitionSO item) =>
            new(
                item.ToDefinition(),
                item.DisplayName,
                item.Description,
                tags: item.Tags);
    }
}
