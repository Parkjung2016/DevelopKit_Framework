using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [Serializable]
    public struct InventorySlotSaveData
    {
        public int SlotIndex;
        public int ItemId;
        public int Count;
        public long InstanceId;
    }

    [Serializable]
    public class InventoryContainerSaveData
    {
        public int Version = InventorySaveVersions.Current;
        public string ContainerId;
        public ContainerKind Kind;
        public InventorySlotSaveData[] Slots = Array.Empty<InventorySlotSaveData>();
    }

    [Serializable]
    public class InventoryGroupSaveData
    {
        public int Version = InventorySaveVersions.Current;
        public InventoryContainerSaveData[] Containers = Array.Empty<InventoryContainerSaveData>();
    }
}
