using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct ItemStack
    {
        private readonly int itemId;
        private readonly int count;
        private readonly long instanceId;

        public int ItemId => itemId;
        public int Count => count;
        public long InstanceId => instanceId;
        public ItemInstanceId Instance => new(instanceId);
        public bool IsEmpty => itemId <= 0 || count <= 0;

        public ItemStack(int itemId, int count, long instanceId = 0)
        {
            this.itemId = itemId;
            this.count = count;
            this.instanceId = instanceId;
        }

        internal static ItemStack From(SlotData data) => new(data.ItemId, data.Count, data.InstanceId);

        public bool CanStackWith(in ItemStack other) =>
            !IsEmpty &&
            !other.IsEmpty &&
            itemId == other.itemId &&
            instanceId <= 0 &&
            other.instanceId <= 0;
    }
}
