namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IContainerCapacityRule
    {
        bool CanAdd(in ItemDefinition definition, int count, int currentItemCount, int occupiedSlotCount);
    }
}
