using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>InventoryGroup 기반 장비 슬롯 조작 API입니다.</summary>
    public sealed class EquipmentSystem
    {
        private readonly InventoryGroup group;
        private readonly string equipmentContainerId;
        private readonly IEquipmentEffectApplier effectApplier;

        public event Action<EquipmentChangeEventArgs> OnEquipmentChanged;

        public EquipmentSystem(
            InventoryGroup group,
            EquipmentSetupSO setup,
            IEquipmentEffectApplier effectApplier = null)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            this.group = group;
            equipmentContainerId = setup.ContainerId;
            this.effectApplier = effectApplier ?? NullEquipmentEffectApplier.Instance;

            if (!group.TryGetContainer(equipmentContainerId, out _))
                throw new InvalidOperationException($"InventoryGroup does not contain equipment container '{equipmentContainerId}'.");
        }

        public string EquipmentContainerId => equipmentContainerId;

        public bool TryGetEquipmentContainer(out InventoryContainer container) =>
            group.TryGetContainer(equipmentContainerId, out container);

        public bool TryGetEquippedSlot(int equipSlotIndex, out InventorySlot slot)
        {
            slot = default;
            if (!TryGetEquipmentContainer(out InventoryContainer container))
                return false;

            return container.TryGetSlot(equipSlotIndex, out slot);
        }

        public InventoryChangeResult TryEquipFromContainer(
            string sourceContainerId,
            int sourceSlotIndex,
            int equipSlotIndex)
        {
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

            ApplyEquipEffects(equipSlotIndex, previousStack, currentSlot.Stack, wasEquipped);
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
            if (!TryGetEquipmentContainer(out InventoryContainer equipmentContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, primarySlotIndex: equipSlotIndex);

            if (!equipmentContainer.TryGetSlot(equipSlotIndex, out InventorySlot equipSlot) || equipSlot.IsEmpty)
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.NoChange, primarySlotIndex: equipSlotIndex);

            ItemStack previousStack = equipSlot.Stack;

            InventoryChangeResult result = group.TryMoveBetween(equipmentContainerId, equipSlotIndex, targetContainerId, targetSlotIndex);
            if (!result.Success)
                return result;

            ApplyUnequipEffect(equipSlotIndex, previousStack);
            RaiseChanged(
                EquipmentChangeType.Unequip,
                equipSlotIndex,
                previousStack,
                default,
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

            RefreshSlotEffects(equipSlotA, previousA);
            RefreshSlotEffects(equipSlotB, previousB);

            if (!equipmentContainer.TryGetSlot(equipSlotA, out slotA))
                return result;

            if (!equipmentContainer.TryGetSlot(equipSlotB, out slotB))
                return result;

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

        private void ApplyEquipEffects(int equipSlotIndex, in ItemStack previousStack, in ItemStack currentStack, bool wasEquipped)
        {
            if (wasEquipped && !previousStack.IsEmpty)
                ApplyUnequipEffect(equipSlotIndex, previousStack);

            if (!currentStack.IsEmpty && group.ItemDatabase.TryGetDefinition(currentStack.ItemId, out ItemDefinition definition))
                effectApplier.OnEquipped(equipSlotIndex, currentStack, definition);
        }

        private void ApplyUnequipEffect(int equipSlotIndex, in ItemStack previousStack)
        {
            if (previousStack.IsEmpty)
                return;

            if (group.ItemDatabase.TryGetDefinition(previousStack.ItemId, out ItemDefinition definition))
                effectApplier.OnUnequipped(equipSlotIndex, previousStack, definition);
        }

        private void RefreshSlotEffects(int equipSlotIndex, in ItemStack previousStack)
        {
            ApplyUnequipEffect(equipSlotIndex, previousStack);

            if (!TryGetEquipmentContainer(out InventoryContainer equipmentContainer))
                return;

            if (!equipmentContainer.TryGetSlot(equipSlotIndex, out InventorySlot currentSlot) || currentSlot.IsEmpty)
                return;

            if (group.ItemDatabase.TryGetDefinition(currentSlot.Stack.ItemId, out ItemDefinition definition))
                effectApplier.OnEquipped(equipSlotIndex, currentSlot.Stack, definition);
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
