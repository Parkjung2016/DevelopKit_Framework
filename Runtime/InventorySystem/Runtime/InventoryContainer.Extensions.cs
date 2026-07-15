using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
        #region Internal helpers

        private InventoryChangeResult TryAddUniqueItems(int itemId, int count, ItemDefinition definition)
        {
            changedSlots.Clear();
            slotSnapshotsBefore.Clear();
            int remainder = count;
            int addedTotal = 0;
            int totalBefore = GetItemCount(itemId);
            bool hadItemBefore = totalBefore > 0;

            for (int i = 0; i < SlotCount && remainder > 0; i++)
            {
                if (!slots[i].IsEmpty || !CanAcceptSlot(i, definition))
                    continue;

                long instanceId = AllocateInstanceId(itemId);
                if (!InventoryBurstOperations.TryPlaceItemInEmptySlot(
                        ref slots, i, itemId, 1, instanceId, ref changedSlots, ref slotSnapshotsBefore, false))
                    continue;

                addedTotal++;
                remainder--;
            }

            if (addedTotal <= 0)
                return Fail(InventoryChangeType.Add, ResolveSlotRuleDeniedReason(definition), itemId, count, totalBefore);

            return CreateSuccess(
                InventoryChangeType.Add,
                itemId,
                count,
                addedTotal,
                remainder,
                totalBefore,
                !hadItemBefore,
                false,
                knownDefinition: definition);
        }

        private int SimulateAddWithSlotRule(int itemId, int count, ItemDefinition definition)
        {
            int remainder = count;

            if (definition.IsStackable)
            {
                for (int i = 0; i < SlotCount && remainder > 0; i++)
                {
                    SlotData slot = slots[i];
                    if (slot.IsEmpty || slot.ItemId != itemId || slot.InstanceId != 0 || !CanAcceptSlot(i, definition))
                        continue;

                    SlotData simulated = slot;
                    remainder = SimulateSlotAdd(ref simulated, itemId, remainder, definition.MaxStackSize);
                }
            }

            for (int i = 0; i < SlotCount && remainder > 0; i++)
            {
                if (!slots[i].IsEmpty || !CanAcceptSlot(i, definition))
                    continue;

                if (definition.IsStackable)
                {
                    SlotData simulated = default;
                    remainder = SimulateSlotAdd(ref simulated, itemId, remainder, definition.MaxStackSize);
                }
                else
                {
                    remainder--;
                }
            }

            return count - remainder;
        }

        private static int SimulateSlotAdd(ref SlotData slot, int itemId, int amount, int maxStackSize)
        {
            if (slot.IsEmpty)
            {
                int added = amount < maxStackSize ? amount : maxStackSize;
                slot.ItemId = itemId;
                slot.Count = added;
                return amount - added;
            }

            if (slot.ItemId != itemId || slot.InstanceId != 0)
                return amount;

            int remainingSpace = maxStackSize - slot.Count;
            if (remainingSpace <= 0)
                return amount;

            int addable = amount < remainingSpace ? amount : remainingSpace;
            slot.Count += addable;
            return amount - addable;
        }

        private bool PassesCapacityRule(in ItemDefinition definition, int count, out InventoryFailReason reason)
        {
            if (capacityRule == null)
            {
                reason = InventoryFailReason.None;
                return true;
            }

            if (capacityRule is IContainerCapacityRuleEx extendedRule)
            {
                if (extendedRule.CanAdd(this, definition, count))
                {
                    reason = InventoryFailReason.None;
                    return true;
                }

                reason = ResolveCapacityDeniedReason();
                return false;
            }

            GetItemAndOccupiedCount(definition.ItemId, out int itemCount, out int occupiedCount);
            if (capacityRule.CanAdd(definition, count, itemCount, occupiedCount))
            {
                reason = InventoryFailReason.None;
                return true;
            }

            reason = InventoryFailReason.CapacityRuleDenied;
            return false;
        }

        private InventoryFailReason ResolveSlotRuleDeniedReason(in ItemDefinition definition)
        {
            if (usesItemTypeSlotRule)
                return InventoryFailReason.ItemTypeNotAllowed;

            return InventoryFailReason.SlotRuleDenied;
        }

        internal int SimulateAddWithoutCapacityCheck(in ItemDefinition definition, int count)
        {
            if (count <= 0)
                return 0;

            return UsesCustomSlotRule
                ? SimulateAddWithSlotRule(definition.ItemId, count, definition)
                : InventoryBurstOperations.SimulateAddItem(
                    ref slots,
                    definition.ItemId,
                    count,
                    definition.MaxStackSize,
                    definition.IsStackable);
        }

        private void GetItemAndOccupiedCount(int itemId, out int itemCount, out int occupiedCount)
        {
            itemCount = 0;
            occupiedCount = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                occupiedCount++;
                if (slot.ItemId == itemId)
                    itemCount += slot.Count;
            }
        }

        private InventoryFailReason ResolveCapacityDeniedReason()
        {
            return capacityRule switch
            {
                WeightCapacityRule => InventoryFailReason.WeightLimitExceeded,
                SlotCountCapacityRule => InventoryFailReason.OccupiedSlotLimitReached,
                _ => InventoryFailReason.CapacityRuleDenied
            };
        }

        private long GenerateInstanceId(int itemId) => InstanceIdGenerator.Generate(itemId);

        #endregion
    }
}
