using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_LootTableDatabase", menuName = "SO/InventorySystem/LootTableDatabase")]
    public class LootTableDatabaseSO : ScriptableObject, ILootTableDatabase
    {
        [field: SerializeField] public LootTableSO[] Tables { get; set; } = System.Array.Empty<LootTableSO>();

        private readonly Dictionary<string, LootTableDefinition> tablesById = new();

        private void OnEnable() => RebuildCache();

        private void OnValidate() => RebuildCache();

        public void RebuildCache()
        {
            tablesById.Clear();
            if (Tables == null)
                return;

            for (int i = 0; i < Tables.Length; i++)
            {
                LootTableSO table = Tables[i];
                if (table == null || string.IsNullOrWhiteSpace(table.TableId))
                    continue;

                if (tablesById.ContainsKey(table.TableId))
                {
                    CDebug.LogWarning($"LootTableDatabaseSO : duplicate table id {table.TableId} in {name}.");
                    continue;
                }

                tablesById.Add(table.TableId, table.ToDefinition());
            }
        }

        public bool TryGetTable(string tableId, out LootTableDefinition table) =>
            tablesById.TryGetValue(tableId, out table);

        public bool TryGetTableAsset(string tableId, out LootTableSO table)
        {
            if (Tables == null)
            {
                table = null;
                return false;
            }

            for (int i = 0; i < Tables.Length; i++)
            {
                LootTableSO candidate = Tables[i];
                if (candidate != null && candidate.TableId == tableId)
                {
                    table = candidate;
                    return true;
                }
            }

            table = null;
            return false;
        }
    }
}
