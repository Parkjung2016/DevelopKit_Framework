using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
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

            if (!TryValidateAddRequest(itemId, count, out ItemDefinition definition, out InventoryFailReason reason))
                return Fail(InventoryChangeType.Add, reason, itemId, count);

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
    }
}
