namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class AnySlotRule : ISlotRule
    {
        public static readonly AnySlotRule Instance = new();

        public bool CanAccept(int slotIndex, in ItemDefinition definition) => true;
    }
}
