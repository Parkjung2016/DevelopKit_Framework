using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public readonly struct EquipmentChangeEventArgs
    {
        public EquipmentChangeType ChangeType { get; }
        public int EquipSlotIndex { get; }
        public ItemStack PreviousStack { get; }
        public ItemStack CurrentStack { get; }
        public string EquipmentContainerId { get; }
        public string SourceContainerId { get; }
        public int SourceSlotIndex { get; }
        public InventoryChangeResult InventoryResult { get; }

        public EquipmentChangeEventArgs(
            EquipmentChangeType changeType,
            int equipSlotIndex,
            in ItemStack previousStack,
            in ItemStack currentStack,
            string equipmentContainerId,
            string sourceContainerId,
            int sourceSlotIndex,
            in InventoryChangeResult inventoryResult)
        {
            ChangeType = changeType;
            EquipSlotIndex = equipSlotIndex;
            PreviousStack = previousStack;
            CurrentStack = currentStack;
            EquipmentContainerId = equipmentContainerId;
            SourceContainerId = sourceContainerId;
            SourceSlotIndex = sourceSlotIndex;
            InventoryResult = inventoryResult;
        }
    }
}
