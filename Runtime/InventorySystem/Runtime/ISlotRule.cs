namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface ISlotRule
    {
        bool CanAccept(int slotIndex, in ItemDefinition definition);
    }
}
