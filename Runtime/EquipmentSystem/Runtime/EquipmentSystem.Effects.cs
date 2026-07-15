using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public sealed partial class EquipmentSystem
    {
        private void ApplySlotTransition(
            int equipSlotIndex,
            in ItemStack previousStack,
            in ItemStack currentStack)
        {
            if (!previousStack.IsEmpty)
                ApplyUnequipEffect(equipSlotIndex, previousStack);

            if (!currentStack.IsEmpty
                && group.ItemDatabase.TryGetDefinition(currentStack.ItemId, out ItemDefinition definition))
            {
                effectApplier.OnEquipped(equipSlotIndex, currentStack, definition);
            }
        }

        private void ApplyUnequipEffect(int equipSlotIndex, in ItemStack previousStack)
        {
            if (previousStack.IsEmpty)
                return;

            if (group.ItemDatabase.TryGetDefinition(previousStack.ItemId, out ItemDefinition definition))
                effectApplier.OnUnequipped(equipSlotIndex, previousStack, definition);
        }

        private void RaiseChanged(
            EquipmentChangeType changeType,
            int equipSlotIndex,
            in ItemStack previousStack,
            in ItemStack currentStack,
            string sourceContainerId,
            int sourceSlotIndex,
            in InventoryChangeResult inventoryResult)
        {
            OnEquipmentChanged?.Invoke(new EquipmentChangeEventArgs(
                changeType,
                equipSlotIndex,
                previousStack,
                currentStack,
                equipmentContainerId,
                sourceContainerId,
                sourceSlotIndex,
                inventoryResult));
        }
    }
}