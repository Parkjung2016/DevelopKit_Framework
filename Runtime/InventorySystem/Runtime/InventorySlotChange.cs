using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct InventorySlotChange
    {
        public int SlotIndex { get; }
        public int PreviousItemId { get; }
        public int PreviousCount { get; }
        public long PreviousInstanceId { get; }
        public int CurrentItemId { get; }
        public int CurrentCount { get; }
        public long CurrentInstanceId { get; }
        public int CountDelta { get; }

        public bool BecameEmpty => CurrentItemId <= 0 || CurrentCount <= 0;
        public bool WasEmpty => PreviousItemId <= 0 || PreviousCount <= 0;
        public bool ItemIdChanged => PreviousItemId != CurrentItemId;

        public InventorySlotChange(
            int slotIndex,
            int previousItemId,
            int previousCount,
            long previousInstanceId,
            int currentItemId,
            int currentCount,
            long currentInstanceId,
            int countDelta)
        {
            SlotIndex = slotIndex;
            PreviousItemId = previousItemId;
            PreviousCount = previousCount;
            PreviousInstanceId = previousInstanceId;
            CurrentItemId = currentItemId;
            CurrentCount = currentCount;
            CurrentInstanceId = currentInstanceId;
            CountDelta = countDelta;
        }

        internal static InventorySlotChange From(int slotIndex, SlotData before, SlotData after)
        {
            int delta = after.ItemId == before.ItemId
                ? after.Count - before.Count
                : after.Count - (before.IsEmpty ? 0 : before.Count);

            return new InventorySlotChange(
                slotIndex,
                before.ItemId,
                before.Count,
                before.InstanceId,
                after.ItemId,
                after.Count,
                after.InstanceId,
                delta);
        }

        public InventorySlot ToCurrentSlot() =>
            InventorySlot.From(new SlotData
            {
                ItemId = CurrentItemId,
                Count = CurrentCount,
                InstanceId = CurrentInstanceId
            });
    }
}
