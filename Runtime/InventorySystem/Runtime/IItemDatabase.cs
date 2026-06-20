namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IItemDatabase
    {
        bool TryGetDefinition(int itemId, out ItemDefinition definition);
    }
}
