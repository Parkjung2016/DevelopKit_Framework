using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InMemoryLootTableDatabase : ILootTableDatabase
    {
        private readonly Dictionary<string, LootTableDefinition> tables = new();

        public void Clear() => tables.Clear();

        public void Register(in LootTableDefinition table)
        {
            if (string.IsNullOrWhiteSpace(table.TableId))
                return;

            tables[table.TableId] = table;
        }

        public void RegisterRange(IEnumerable<LootTableDefinition> source)
        {
            if (source == null)
                return;

            foreach (LootTableDefinition table in source)
                Register(table);
        }

        public bool TryGetTable(string tableId, out LootTableDefinition table) =>
            tables.TryGetValue(tableId, out table);
    }
}
