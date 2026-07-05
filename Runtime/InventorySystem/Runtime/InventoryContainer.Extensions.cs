using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
        #region Validation (Dry-run)

        public bool CanAddItem(int itemId, int count, out InventoryFailReason reason, out int addableCount)
        {
            addableCount = 0;
            reason = InventoryFailReason.None;

            if (isDisposed || itemDatabase == null)
            {
                reason = InventoryFailReason.DatabaseNotReady;
                return false;
            }

            if (itemId <= 0)
            {
                reason = InventoryFailReason.InvalidItemId;
                return false;
            }

            if (count <= 0)
            {
                reason = InventoryFailReason.InvalidCount;
                return false;
            }

            if (!TryGetDefinition(itemId, out ItemDefinition definition))
            {
                reason = InventoryFailReason.DefinitionNotFound;
                return false;
            }

            if (!PassesCapacityRule(definition, count, out reason))
                return false;

            addableCount = UsesCustomSlotRule
                ? SimulateAddWithSlotRule(itemId, count, definition)
                : InventoryBurstOperations.SimulateAddItem(
                    ref slots,
                    itemId,
                    count,
                    definition.MaxStackSize,
                    definition.IsStackable);

            if (addableCount <= 0)
            {
                reason = UsesCustomSlotRule
                    ? ResolveSlotRuleDeniedReason(definition)
                    : InventoryFailReason.NoSpace;
                return false;
            }

            reason = InventoryFailReason.None;
            return addableCount >= count;
        }

        public bool CanRemoveItem(int itemId, int count, out InventoryFailReason reason)
        {
            reason = InventoryFailReason.None;

            if (itemId <= 0)
            {
                reason = InventoryFailReason.InvalidItemId;
                return false;
            }

            if (count <= 0)
            {
                reason = InventoryFailReason.InvalidCount;
                return false;
            }

            int currentCount = GetItemCount(itemId);
            if (currentCount <= 0)
            {
                reason = InventoryFailReason.ItemNotFound;
                return false;
            }

            if (currentCount < count)
            {
                reason = InventoryFailReason.InsufficientItemCount;
                return false;
            }

            return true;
        }

        public bool CanMoveSlot(int fromSlotIndex, int toSlotIndex, out InventoryFailReason reason)
        {
            reason = InventoryFailReason.None;

            if (fromSlotIndex < 0 || fromSlotIndex >= SlotCount || toSlotIndex < 0 || toSlotIndex >= SlotCount)
            {
                reason = InventoryFailReason.InvalidSlotIndex;
                return false;
            }

            if (fromSlotIndex == toSlotIndex || slots[fromSlotIndex].IsEmpty)
            {
                reason = InventoryFailReason.NoChange;
                return false;
            }

            int itemId = slots[fromSlotIndex].ItemId;
            if (!TryGetDefinition(itemId, out ItemDefinition definition))
            {
                reason = InventoryFailReason.DefinitionNotFound;
                return false;
            }

            SlotData toSlot = slots[toSlotIndex];
            if (toSlot.IsEmpty && !CanAcceptSlot(toSlotIndex, definition))
            {
                reason = InventoryFailReason.SlotRuleDenied;
                return false;
            }

            if (!toSlot.IsEmpty && toSlot.ItemId == itemId && !CanAcceptSlot(toSlotIndex, definition))
            {
                reason = InventoryFailReason.SlotRuleDenied;
                return false;
            }

            return true;
        }

        public bool CanSplitStack(int slotIndex, int splitCount, out InventoryFailReason reason, out int targetSlotIndex)
        {
            targetSlotIndex = -1;
            reason = InventoryFailReason.None;

            if (splitCount <= 0)
            {
                reason = InventoryFailReason.InvalidCount;
                return false;
            }

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                reason = InventoryFailReason.InvalidSlotIndex;
                return false;
            }

            SlotData slot = slots[slotIndex];
            if (slot.IsEmpty || slot.Count <= splitCount || slot.InstanceId != 0)
            {
                reason = InventoryFailReason.NoChange;
                return false;
            }

            targetSlotIndex = GetFirstEmptySlotIndex();
            if (targetSlotIndex < 0)
            {
                reason = InventoryFailReason.CapacityRuleDenied;
                return false;
            }

            return true;
        }

        #endregion

        #region Query

        public int GetFirstEmptySlotIndex() =>
            isDisposed ? -1 : InventoryBurstOperations.FindFirstEmptySlotIndex(ref slots);

        public int GetOccupiedSlotCount() =>
            isDisposed ? 0 : InventoryBurstOperations.GetOccupiedSlotCount(ref slots);

        public void FindSlotsWithItem(int itemId, List<int> results)
        {
            results.Clear();
            if (isDisposed || itemId <= 0)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemId == itemId)
                    results.Add(i);
            }
        }

        public bool TryFindStackableSlot(int itemId, out int slotIndex)
        {
            slotIndex = -1;
            if (isDisposed || itemId <= 0 || itemDatabase == null)
                return false;

            if (!TryGetDefinition(itemId, out ItemDefinition definition) || !definition.IsStackable)
                return false;

            int maxStack = definition.MaxStackSize;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.ItemId == itemId && slot.InstanceId == 0 && slot.Count < maxStack)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Weight / capacity helpers

        public float GetTotalWeight()
        {
            if (isDisposed || itemDatabase == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                if (!TryGetDefinition(slot.ItemId, out ItemDefinition definition))
                    continue;

                total += definition.Weight * slot.Count;
            }

            return total;
        }

        #endregion

        #region Item Actions

        public InventoryChangeResult TrySplitStack(int slotIndex, int splitCount)
        {
            if (!CanSplitStack(slotIndex, splitCount, out InventoryFailReason reason, out int targetSlotIndex))
                return Fail(InventoryChangeType.Split, reason, primarySlotIndex: slotIndex, secondarySlotIndex: targetSlotIndex);

            int itemId = slots[slotIndex].ItemId;
            int totalBefore = GetItemCount(itemId);

            bool split = InventoryBurstOperations.TrySplitStack(
                ref slots,
                slotIndex,
                targetSlotIndex,
                splitCount,
                ref changedSlots,
                ref slotSnapshotsBefore,
                out int processedCount);

            if (!split)
                return Fail(InventoryChangeType.Split, InventoryFailReason.NoChange, itemId, splitCount, totalBefore, slotIndex, targetSlotIndex);

            return CreateSuccess(
                InventoryChangeType.Split,
                itemId,
                splitCount,
                processedCount,
                0,
                totalBefore,
                false,
                false,
                slotIndex,
                targetSlotIndex);
        }

        public InventoryChangeResult TryDropItemFromSlot(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || slots[slotIndex].IsEmpty)
                return Fail(InventoryChangeType.Drop, InventoryFailReason.NoChange, primarySlotIndex: slotIndex);

            int itemId = slots[slotIndex].ItemId;
            if (!TryGetDefinition(itemId, out ItemDefinition definition))
                return Fail(InventoryChangeType.Drop, InventoryFailReason.DefinitionNotFound, itemId, primarySlotIndex: slotIndex);

            if (!definition.CanDrop)
                return Fail(InventoryChangeType.Drop, InventoryFailReason.ItemActionDenied, itemId, count, primarySlotIndex: slotIndex);

            int totalBefore = GetItemCount(itemId);

            bool removed = InventoryBurstOperations.TryRemoveItemFromSlot(
                ref slots,
                slotIndex,
                count,
                ref changedSlots,
                ref slotSnapshotsBefore,
                true,
                out int removedCount,
                out int remainder);

            if (!removed)
                return Fail(InventoryChangeType.Drop, InventoryFailReason.NoChange, itemId, count, totalBefore, slotIndex);

            return CreateSuccess(
                InventoryChangeType.Drop,
                itemId,
                count,
                removedCount,
                remainder,
                totalBefore,
                false,
                totalBefore <= removedCount,
                slotIndex);
        }

        public InventoryChangeResult TryTradeItemFromSlot(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || slots[slotIndex].IsEmpty)
                return Fail(InventoryChangeType.Trade, InventoryFailReason.NoChange, primarySlotIndex: slotIndex);

            int itemId = slots[slotIndex].ItemId;
            if (!TryGetDefinition(itemId, out ItemDefinition definition))
                return Fail(InventoryChangeType.Trade, InventoryFailReason.DefinitionNotFound, itemId, primarySlotIndex: slotIndex);

            if (!definition.CanTrade)
                return Fail(InventoryChangeType.Trade, InventoryFailReason.ItemActionDenied, itemId, count, primarySlotIndex: slotIndex);

            int totalBefore = GetItemCount(itemId);

            bool removed = InventoryBurstOperations.TryRemoveItemFromSlot(
                ref slots,
                slotIndex,
                count,
                ref changedSlots,
                ref slotSnapshotsBefore,
                true,
                out int removedCount,
                out int remainder);

            if (!removed)
                return Fail(InventoryChangeType.Trade, InventoryFailReason.NoChange, itemId, count, totalBefore, slotIndex);

            return CreateSuccess(
                InventoryChangeType.Trade,
                itemId,
                count,
                removedCount,
                remainder,
                totalBefore,
                false,
                totalBefore <= removedCount,
                slotIndex);
        }

        public InventoryChangeResult TryUseItem(int slotIndex, IItemUseHandler handler)
        {
            if (handler == null)
                return Fail(InventoryChangeType.Use, InventoryFailReason.DatabaseNotReady, primarySlotIndex: slotIndex);

            if (slotIndex < 0 || slotIndex >= SlotCount || slots[slotIndex].IsEmpty)
                return Fail(InventoryChangeType.Use, InventoryFailReason.NoChange, primarySlotIndex: slotIndex);

            int itemId = slots[slotIndex].ItemId;
            if (!TryGetDefinition(itemId, out ItemDefinition definition))
                return Fail(InventoryChangeType.Use, InventoryFailReason.DefinitionNotFound, itemId, primarySlotIndex: slotIndex);

            if (!handler.CanUse(this, slotIndex, definition))
                return Fail(InventoryChangeType.Use, InventoryFailReason.ItemActionDenied, itemId, primarySlotIndex: slotIndex);

            return handler.TryUse(this, slotIndex);
        }

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count, long instanceId)
        {
            if (instanceId <= 0)
                return TryAddItemToSlot(slotIndex, itemId, count);

            if (count != 1)
                return Fail(InventoryChangeType.Add, InventoryFailReason.InvalidCount, itemId, count, primarySlotIndex: slotIndex);

            if (isDisposed || itemDatabase == null)
                return Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (!TryGetDefinition(itemId, out ItemDefinition definition))
                return Fail(InventoryChangeType.Add, InventoryFailReason.DefinitionNotFound, itemId, count);

            if (slotIndex < 0 || slotIndex >= SlotCount || !slots[slotIndex].IsEmpty)
                return Fail(InventoryChangeType.Add, InventoryFailReason.NoSpace, itemId, count, primarySlotIndex: slotIndex);

            if (!CanAcceptSlot(slotIndex, definition))
                return Fail(InventoryChangeType.Add, ResolveSlotRuleDeniedReason(definition), itemId, count, primarySlotIndex: slotIndex);

            EnsureInstanceRegistered(itemId, instanceId);

            int totalBefore = GetItemCount(itemId);
            changedSlots.Clear();
            slotSnapshotsBefore.Clear();

            bool placed = InventoryBurstOperations.TryPlaceItemInEmptySlot(
                ref slots, slotIndex, itemId, 1, instanceId, ref changedSlots, ref slotSnapshotsBefore, true);

            if (!placed)
                return Fail(InventoryChangeType.Add, InventoryFailReason.NoSpace, itemId, count, totalBefore, slotIndex);

            return CreateSuccess(
                InventoryChangeType.Add,
                itemId,
                1,
                1,
                0,
                totalBefore,
                totalBefore <= 0,
                false,
                slotIndex,
                knownDefinition: definition);
        }

        #endregion

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
