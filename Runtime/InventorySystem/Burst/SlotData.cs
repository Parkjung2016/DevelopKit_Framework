using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Burst
{
    [Serializable]
    public struct SlotData
    {
        public int ItemId;
        public int Count;
        public long InstanceId;

        public bool IsEmpty => ItemId <= 0 || Count <= 0;

        public bool IsFungibleStack => InstanceId <= 0;

        public static SlotData Empty => default;

        public static SlotData Create(int itemId, int count, long instanceId = 0) =>
            new() { ItemId = itemId, Count = count, InstanceId = instanceId };
    }
}
