using System;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [Serializable]
    public struct InventorySlotDelta
    {
        public int SlotIndex;
        public int PreviousItemId;
        public int PreviousCount;
        public long PreviousInstanceId;
        public int CurrentItemId;
        public int CurrentCount;
        public long CurrentInstanceId;

        public static InventorySlotDelta FromChange(int slotIndex, in SlotData before, in SlotData after) =>
            new()
            {
                SlotIndex = slotIndex,
                PreviousItemId = before.ItemId,
                PreviousCount = before.Count,
                PreviousInstanceId = before.InstanceId,
                CurrentItemId = after.ItemId,
                CurrentCount = after.Count,
                CurrentInstanceId = after.InstanceId
            };
    }

    [Serializable]
    public class InventoryContainerDelta
    {
        public string ContainerId;
        public int Revision;
        public InventorySlotDelta[] Slots = Array.Empty<InventorySlotDelta>();
    }

    [Serializable]
    public class InventoryGroupDelta
    {
        public InventoryContainerDelta[] Containers = Array.Empty<InventoryContainerDelta>();
    }
}
