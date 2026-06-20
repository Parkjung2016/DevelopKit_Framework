using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct LootTableDefinition
    {
        public string TableId { get; }
        public LootEntry[] Entries { get; }
        public int RollCount { get; }
        public bool AllowDuplicateRolls { get; }

        public LootTableDefinition(
            string tableId,
            LootEntry[] entries,
            int rollCount = 1,
            bool allowDuplicateRolls = true)
        {
            TableId = tableId;
            Entries = entries ?? Array.Empty<LootEntry>();
            RollCount = rollCount < 1 ? 1 : rollCount;
            AllowDuplicateRolls = allowDuplicateRolls;
        }
    }
}
