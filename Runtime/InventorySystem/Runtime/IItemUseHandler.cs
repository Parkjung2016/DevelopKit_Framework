namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IItemUseHandler
    {
        bool CanUse(IInventoryContainer container, int slotIndex, in ItemDefinition definition);

        InventoryChangeResult TryUse(IInventoryContainer container, int slotIndex);
    }
}
