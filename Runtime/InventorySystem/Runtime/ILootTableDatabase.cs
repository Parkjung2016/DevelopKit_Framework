namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface ILootTableDatabase
    {
        bool TryGetTable(string tableId, out LootTableDefinition table);
    }
}
