namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class NullLootTableDatabase : ILootTableDatabase
    {
        public static readonly NullLootTableDatabase Instance = new();

        private NullLootTableDatabase() { }

        public bool TryGetTable(string tableId, out LootTableDefinition table)
        {
            table = default;
            return false;
        }
    }
}
