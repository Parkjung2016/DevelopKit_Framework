using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct InventorySlot
    {
        private readonly int itemId;
        private readonly int count;
        private readonly long instanceId;

        public ItemStack Stack => ItemStack.From(new SlotData { ItemId = itemId, Count = count, InstanceId = instanceId });
        public bool IsEmpty => itemId <= 0 || count <= 0;
        public long InstanceId => instanceId;

        internal static InventorySlot From(SlotData data) => new(data.ItemId, data.Count, data.InstanceId);

        private InventorySlot(int itemId, int count, long instanceId)
        {
            this.itemId = itemId;
            this.count = count;
            this.instanceId = instanceId;
        }
    }
}
