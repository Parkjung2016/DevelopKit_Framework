using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public sealed partial class EquipmentSystem
    {
        public InventoryChangeResult TryEquipFromContainer(
            string sourceContainerId,
            int sourceSlotIndex,
            int equipSlotIndex)
        {
            if (string.Equals(sourceContainerId, equipmentContainerId, StringComparison.Ordinal))
                return TrySwapEquippedSlots(sourceSlotIndex, equipSlotIndex);

            if (!TryGetEquipmentContainer(out InventoryContainer equipmentContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, secondarySlotIndex: equipSlotIndex);

            if (!equipmentContainer.TryGetSlot(equipSlotIndex, out InventorySlot equipSlot))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.InvalidSlotIndex, secondarySlotIndex: equipSlotIndex);

            ItemStack previousStack = equipSlot.Stack;
            bool wasEquipped = !equipSlot.IsEmpty;

            InventoryChangeResult result = group.TryMoveBetween(sourceContainerId, sourceSlotIndex, equipmentContainerId, equipSlotIndex);
            if (!result.Success)
                return result;

            if (!equipmentContainer.TryGetSlot(equipSlotIndex, out InventorySlot currentSlot))
                return result;

            ApplySlotTransition(equipSlotIndex, previousStack, currentSlot.Stack);
            RaiseChanged(
                wasEquipped ? EquipmentChangeType.Swap : EquipmentChangeType.Equip,
                equipSlotIndex,
                previousStack,
                currentSlot.Stack,
                sourceContainerId,
                sourceSlotIndex,
                result);

            return result;
        }

        public InventoryChangeResult TryUnequipToContainer(
            int equipSlotIndex,
            string targetContainerId,
            int targetSlotIndex)
        {
            if (string.Equals(targetContainerId, equipmentContainerId, StringComparison.Ordinal))
                return TrySwapEquippedSlots(equipSlotIndex, targetSlotIndex);

            if (!TryGetEquipmentContainer(out InventoryContainer equipmentContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, primarySlotIndex: equipSlotIndex);

            if (!equipmentContainer.TryGetSlot(equipSlotIndex, out InventorySlot equipSlot) || equipSlot.IsEmpty)
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.NoChange, primarySlotIndex: equipSlotIndex);

            ItemStack previousStack = equipSlot.Stack;
            InventoryChangeResult result = group.TryMoveBetween(
                equipmentContainerId,
                equipSlotIndex,
                targetContainerId,
                targetSlotIndex);
            if (!result.Success)
                return result;

            if (!equipmentContainer.TryGetSlot(equipSlotIndex, out InventorySlot currentSlot))
                return result;

            ApplySlotTransition(equipSlotIndex, previousStack, currentSlot.Stack);
            RaiseChanged(
                currentSlot.IsEmpty ? EquipmentChangeType.Unequip : EquipmentChangeType.Swap,
                equipSlotIndex,
                previousStack,
                currentSlot.Stack,
                targetContainerId,
                targetSlotIndex,
                result);

            return result;
        }

        public InventoryChangeResult TryUnequipToFirstAvailable(int equipSlotIndex, string targetContainerId)
        {
            if (!group.TryGetContainer(targetContainerId, out InventoryContainer targetContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, primarySlotIndex: equipSlotIndex);

            int emptySlot = targetContainer.GetFirstEmptySlotIndex();
            if (emptySlot < 0)
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.NoSpace, primarySlotIndex: equipSlotIndex);

            return TryUnequipToContainer(equipSlotIndex, targetContainerId, emptySlot);
        }

        public InventoryChangeResult TrySwapEquippedSlots(int equipSlotA, int equipSlotB)
        {
            if (!TryGetEquipmentContainer(out InventoryContainer equipmentContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.ContainerNotFound);

            if (!equipmentContainer.TryGetSlot(equipSlotA, out InventorySlot slotA))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.InvalidSlotIndex, primarySlotIndex: equipSlotA, secondarySlotIndex: equipSlotB);

            if (!equipmentContainer.TryGetSlot(equipSlotB, out InventorySlot slotB))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.InvalidSlotIndex, primarySlotIndex: equipSlotA, secondarySlotIndex: equipSlotB);

            if (!CanSwapEquippedSlots(equipmentContainer, equipSlotA, slotA, equipSlotB, slotB, out InventoryFailReason denyReason))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, denyReason, primarySlotIndex: equipSlotA, secondarySlotIndex: equipSlotB);

            ItemStack previousA = slotA.Stack;
            ItemStack previousB = slotB.Stack;

            InventoryChangeResult result = group.TrySwapBetween(equipmentContainerId, equipSlotA, equipmentContainerId, equipSlotB);
            if (!result.Success)
                return result;

            if (!equipmentContainer.TryGetSlot(equipSlotA, out slotA)
                || !equipmentContainer.TryGetSlot(equipSlotB, out slotB))
                return result;

            ApplySlotTransition(equipSlotA, previousA, slotA.Stack);
            ApplySlotTransition(equipSlotB, previousB, slotB.Stack);
            RaiseChanged(
                EquipmentChangeType.Swap,
                equipSlotA,
                previousA,
                slotA.Stack,
                equipmentContainerId,
                equipSlotB,
                result);

            return result;
        }

        private bool CanSwapEquippedSlots(
            InventoryContainer equipmentContainer,
            int equipSlotA,
            in InventorySlot slotA,
            int equipSlotB,
            in InventorySlot slotB,
            out InventoryFailReason reason)
        {
            reason = InventoryFailReason.None;

            if (!slotA.IsEmpty)
            {
                if (!group.ItemDatabase.TryGetDefinition(slotA.Stack.ItemId, out ItemDefinition definitionA))
                {
                    reason = InventoryFailReason.DefinitionNotFound;
                    return false;
                }

                if (!equipmentContainer.CanAcceptSlot(equipSlotB, definitionA))
                {
                    reason = InventoryFailReason.SlotRuleDenied;
                    return false;
                }
            }

            if (!slotB.IsEmpty)
            {
                if (!group.ItemDatabase.TryGetDefinition(slotB.Stack.ItemId, out ItemDefinition definitionB))
                {
                    reason = InventoryFailReason.DefinitionNotFound;
                    return false;
                }

                if (!equipmentContainer.CanAcceptSlot(equipSlotA, definitionB))
                {
                    reason = InventoryFailReason.SlotRuleDenied;
                    return false;
                }
            }

            return true;
        }
    }
}