using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
        #region Add Item

        public InventoryChangeResult TryAddItem(int itemId, int count)
        {
            if (!TryValidateAddRequest(itemId, count, out ItemDefinition definition, out InventoryFailReason reason))
                return Fail(InventoryChangeType.Add, reason, itemId, count);

            if (!PassesCapacityRule(definition, count, out InventoryFailReason capacityReason))
                return Fail(InventoryChangeType.Add, capacityReason, itemId, count);

            if (!definition.IsStackable)
                return TryAddUniqueItems(itemId, count, definition);

            if (UsesCustomSlotRule)
            {
                int totalBeforeCustom = GetItemCount(itemId);
                return TryAddItemWithSlotRule(itemId, count, definition, totalBeforeCustom, totalBeforeCustom > 0);
            }

            InventoryBurstOperations.TryAddItem(
                ref slots,
                itemId,
                count,
                definition.MaxStackSize,
                definition.IsStackable,
                ref changedSlots,
                ref slotSnapshotsBefore,
                out int addedTotal,
                out int remainder,
                out int totalBefore);

            bool hadItemBefore = totalBefore > 0;

            if (addedTotal <= 0)
                return Fail(InventoryChangeType.Add, InventoryFailReason.NoSpace, itemId, count, totalBefore);

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

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count)
        {
            if (!TryValidateAddRequest(itemId, count, out ItemDefinition definition, out InventoryFailReason reason))
                return Fail(InventoryChangeType.Add, reason, itemId, count);

            if (slotIndex < 0 || slotIndex >= SlotCount)
                return Fail(InventoryChangeType.Add, InventoryFailReason.InvalidSlotIndex, itemId, count, primarySlotIndex: slotIndex);

            if (!slots[slotIndex].IsEmpty && slots[slotIndex].ItemId != itemId)
                return Fail(InventoryChangeType.Add, InventoryFailReason.SlotMismatch, itemId, count, primarySlotIndex: slotIndex);

            if (!CanAcceptSlot(slotIndex, definition))
                return Fail(InventoryChangeType.Add, ResolveSlotRuleDeniedReason(definition), itemId, count, primarySlotIndex: slotIndex);

            if (!PassesCapacityRule(definition, count, out InventoryFailReason capacityReason))
                return Fail(InventoryChangeType.Add, capacityReason, itemId, count, primarySlotIndex: slotIndex);

            int totalBefore = GetItemCount(itemId);
            bool hadItemBefore = totalBefore > 0;

            if (!definition.IsStackable)
            {
                if (count != 1 || !slots[slotIndex].IsEmpty)
                    return Fail(InventoryChangeType.Add, InventoryFailReason.NoSpace, itemId, count, totalBefore, slotIndex);

                long instanceId = AllocateInstanceId(itemId);
                changedSlots.Clear();
                slotSnapshotsBefore.Clear();
                bool placed = InventoryBurstOperations.TryPlaceItemInEmptySlot(
                    ref slots, slotIndex, itemId, 1, instanceId, ref changedSlots, ref slotSnapshotsBefore, true);
                if (!placed)
                    return Fail(InventoryChangeType.Add, InventoryFailReason.NoSpace, itemId, count, totalBefore, slotIndex);

                return CreateSuccess(InventoryChangeType.Add, itemId, count, 1, 0, totalBefore, !hadItemBefore, false, slotIndex, knownDefinition: definition);
            }

            changedSlots.Clear();
            slotSnapshotsBefore.Clear();
            bool added = InventoryBurstOperations.TryAddItemToSlot(
                ref slots,
                slotIndex,
                itemId,
                count,
                definition.MaxStackSize,
                definition.IsStackable,
                ref changedSlots,
                ref slotSnapshotsBefore,
                true,
                out int addedTotal,
                out int remainder);

            if (!added)
                return Fail(InventoryChangeType.Add, InventoryFailReason.NoSpace, itemId, count, totalBefore, slotIndex);

            return CreateSuccess(
                InventoryChangeType.Add,
                itemId,
                count,
                addedTotal,
                remainder,
                totalBefore,
                !hadItemBefore,
                false,
                slotIndex,
                knownDefinition: definition);
        }

        #endregion

        #region Remove Item

        public InventoryChangeResult TryRemoveItem(int itemId, int count)
        {
            if (isDisposed)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (itemId <= 0)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.InvalidItemId, itemId, count);

            if (count <= 0)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.InvalidCount, itemId, count);

            int totalBefore = GetItemCount(itemId);
            if (totalBefore <= 0)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.ItemNotFound, itemId, count, totalBefore);

            InventoryBurstOperations.TryRemoveItem(
                ref slots,
                itemId,
                count,
                ref changedSlots,
                ref slotSnapshotsBefore,
                totalBefore,
                out int removedCount,
                out int remainder,
                out _);

            if (removedCount <= 0)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.InsufficientItemCount, itemId, count, totalBefore);

            ProcessReleasedInstances();

            return CreateSuccess(
                InventoryChangeType.Remove,
                itemId,
                count,
                removedCount,
                remainder,
                totalBefore,
                false,
                totalBefore <= removedCount);
        }

        public InventoryChangeResult TryRemoveItemFromSlot(int slotIndex, int count) =>
            TryRemoveItemFromSlotInternal(slotIndex, count, releaseInstance: true);

        internal InventoryChangeResult TryTakeItemFromSlot(int slotIndex, int count) =>
            TryRemoveItemFromSlotInternal(slotIndex, count, releaseInstance: false);

        private InventoryChangeResult TryRemoveItemFromSlotInternal(
            int slotIndex,
            int count,
            bool releaseInstance)
        {
            if (isDisposed)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.DatabaseNotReady, requestedCount: count);

            if (count <= 0)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.InvalidCount, requestedCount: count);

            if (slotIndex < 0 || slotIndex >= SlotCount)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.InvalidSlotIndex, requestedCount: count, primarySlotIndex: slotIndex);

            if (slots[slotIndex].IsEmpty)
                return Fail(InventoryChangeType.Remove, InventoryFailReason.SlotEmpty, primarySlotIndex: slotIndex);

            int itemId = slots[slotIndex].ItemId;
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
                return Fail(InventoryChangeType.Remove, InventoryFailReason.InsufficientItemCount, itemId, count, totalBefore, slotIndex);

            if (releaseInstance)
                ProcessReleasedInstances();

            return CreateSuccess(
                InventoryChangeType.Remove,
                itemId,
                count,
                removedCount,
                remainder,
                totalBefore,
                false,
                totalBefore <= removedCount,
                slotIndex);
        }

        #endregion

        #region Slot Operations

        public InventoryChangeResult TryMoveSlot(int fromSlotIndex, int toSlotIndex)
        {
            if (isDisposed)
                return Fail(InventoryChangeType.Move, InventoryFailReason.DatabaseNotReady);

            if (fromSlotIndex < 0 || fromSlotIndex >= SlotCount ||
                toSlotIndex < 0 || toSlotIndex >= SlotCount)
                return Fail(InventoryChangeType.Move, InventoryFailReason.InvalidSlotIndex, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex);

            if (fromSlotIndex == toSlotIndex || slots[fromSlotIndex].IsEmpty)
                return Fail(InventoryChangeType.Move, InventoryFailReason.NoChange, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex);

            int itemId = slots[fromSlotIndex].ItemId;
            int totalBefore = GetItemCount(itemId);
            bool hadItemBefore = totalBefore > 0;
            SlotData toSlotBefore = slots[toSlotIndex];

            if (!TryGetDefinition(itemId, out ItemDefinition definition))
                return Fail(InventoryChangeType.Move, InventoryFailReason.DefinitionNotFound, itemId, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex, totalItemCountBefore: totalBefore);

            if (!toSlotBefore.IsEmpty && toSlotBefore.ItemId == itemId && !CanAcceptSlot(toSlotIndex, definition))
                return Fail(InventoryChangeType.Move, InventoryFailReason.SlotRuleDenied, itemId, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex, totalItemCountBefore: totalBefore);

            if (toSlotBefore.IsEmpty && !CanAcceptSlot(toSlotIndex, definition))
                return Fail(InventoryChangeType.Move, InventoryFailReason.SlotRuleDenied, itemId, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex, totalItemCountBefore: totalBefore);

            InventoryChangeType changeType = !toSlotBefore.IsEmpty && toSlotBefore.ItemId != itemId
                ? InventoryChangeType.Swap
                : InventoryChangeType.Move;

            int requestedCount = slots[fromSlotIndex].Count;

            bool moved = InventoryBurstOperations.TryMoveSlot(
                ref slots,
                fromSlotIndex,
                toSlotIndex,
                definition.MaxStackSize,
                definition.IsStackable,
                ref changedSlots,
                ref slotSnapshotsBefore,
                out int processedCount,
                out int remainder);

            if (!moved)
                return Fail(changeType, InventoryFailReason.NoChange, itemId, requestedCount, totalBefore, fromSlotIndex, toSlotIndex);

            return CreateSuccess(
                changeType,
                itemId,
                requestedCount,
                processedCount,
                remainder,
                totalBefore,
                false,
                changeType == InventoryChangeType.Swap && hadItemBefore && !HasItem(itemId),
                fromSlotIndex,
                toSlotIndex,
                knownDefinition: definition);
        }

        public InventoryChangeResult TrySwapSlots(int slotIndexA, int slotIndexB)
        {
            if (isDisposed)
                return Fail(InventoryChangeType.Swap, InventoryFailReason.DatabaseNotReady);

            if (slotIndexA < 0 || slotIndexA >= SlotCount ||
                slotIndexB < 0 || slotIndexB >= SlotCount)
                return Fail(InventoryChangeType.Swap, InventoryFailReason.InvalidSlotIndex, primarySlotIndex: slotIndexA, secondarySlotIndex: slotIndexB);

            if (slotIndexA == slotIndexB || (slots[slotIndexA].IsEmpty && slots[slotIndexB].IsEmpty))
                return Fail(InventoryChangeType.Swap, InventoryFailReason.NoChange, primarySlotIndex: slotIndexA, secondarySlotIndex: slotIndexB);

            int itemId = slots[slotIndexA].ItemId;
            int totalBefore = itemId > 0 ? GetItemCount(itemId) : 0;

            bool swapped = InventoryBurstOperations.TrySwapSlots(
                ref slots, slotIndexA, slotIndexB, ref changedSlots, ref slotSnapshotsBefore);
            if (!swapped)
                return Fail(InventoryChangeType.Swap, InventoryFailReason.NoChange, itemId, primarySlotIndex: slotIndexA, secondarySlotIndex: slotIndexB, totalItemCountBefore: totalBefore);

            return CreateSuccess(
                InventoryChangeType.Swap,
                itemId,
                0,
                0,
                0,
                totalBefore,
                false,
                false,
                slotIndexA,
                slotIndexB);
        }

        public InventoryChangeResult ClearSlot(int slotIndex)
        {
            if (isDisposed)
                return Fail(InventoryChangeType.Clear, InventoryFailReason.DatabaseNotReady);

            if (slotIndex < 0 || slotIndex >= SlotCount)
                return Fail(InventoryChangeType.Clear, InventoryFailReason.InvalidSlotIndex, primarySlotIndex: slotIndex);

            if (slots[slotIndex].IsEmpty)
                return Fail(InventoryChangeType.Clear, InventoryFailReason.NoChange, primarySlotIndex: slotIndex);

            int itemId = slots[slotIndex].ItemId;
            int totalBefore = GetItemCount(itemId);
            int requestedCount = slots[slotIndex].Count;

            bool cleared = InventoryBurstOperations.ClearSlot(
                ref slots, slotIndex, ref changedSlots, ref slotSnapshotsBefore, out int clearedCount);
            if (!cleared)
                return Fail(InventoryChangeType.Clear, InventoryFailReason.NoChange, itemId, requestedCount, totalBefore, slotIndex);

            ProcessReleasedInstances();

            return CreateSuccess(
                InventoryChangeType.Clear,
                itemId,
                requestedCount,
                clearedCount,
                0,
                totalBefore,
                false,
                totalBefore <= clearedCount,
                slotIndex);
        }

        public InventoryChangeResult ClearAll()
        {
            if (isDisposed)
                return Fail(InventoryChangeType.Clear, InventoryFailReason.DatabaseNotReady);

            int clearedCount = InventoryBurstOperations.ClearAll(ref slots, ref changedSlots, ref slotSnapshotsBefore);
            if (clearedCount <= 0)
                return Fail(InventoryChangeType.Clear, InventoryFailReason.NoChange);

            ProcessReleasedInstances();

            return CreateSuccess(
                InventoryChangeType.Clear,
                0,
                clearedCount,
                clearedCount,
                0,
                0,
                false,
                false);
        }

        #endregion

        private InventoryChangeResult TryAddItemWithSlotRule(
            int itemId,
            int count,
            ItemDefinition definition,
            int totalBefore,
            bool hadItemBefore)
        {
            changedSlots.Clear();
            slotSnapshotsBefore.Clear();
            int remainder = count;
            int addedTotal = 0;

            if (definition.IsStackable)
            {
                for (int i = 0; i < SlotCount && remainder > 0; i++)
                {
                    if (slots[i].IsEmpty || slots[i].ItemId != itemId || !CanAcceptSlot(i, definition))
                        continue;

                    if (!TryAddToSlotBurst(i, itemId, remainder, definition, resetChangedSlots: false, out int added, out remainder))
                        continue;

                    addedTotal += added;
                }
            }

            for (int i = 0; i < SlotCount && remainder > 0; i++)
            {
                if (!slots[i].IsEmpty || !CanAcceptSlot(i, definition))
                    continue;

                if (!TryAddToSlotBurst(i, itemId, remainder, definition, resetChangedSlots: false, out int added, out remainder))
                    continue;

                addedTotal += added;
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

        private bool TryAddToSlotBurst(
            int slotIndex,
            int itemId,
            int count,
            ItemDefinition definition,
            bool resetChangedSlots,
            out int added,
            out int remainder)
        {
            bool success = InventoryBurstOperations.TryAddItemToSlot(
                ref slots,
                slotIndex,
                itemId,
                count,
                definition.MaxStackSize,
                definition.IsStackable,
                ref changedSlots,
                ref slotSnapshotsBefore,
                resetChangedSlots,
                out added,
                out remainder);

            return success;
        }
    }
}
