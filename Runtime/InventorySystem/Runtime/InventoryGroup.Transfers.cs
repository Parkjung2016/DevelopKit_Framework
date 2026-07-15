namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryGroup
    {
        public InventoryChangeResult TryMoveBetween(
            string fromContainerId,
            int fromSlotIndex,
            string toContainerId,
            int toSlotIndex)
        {
            if (!TryGetContainer(fromContainerId, out InventoryContainer fromContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, primarySlotIndex: fromSlotIndex);

            if (!TryGetContainer(toContainerId, out InventoryContainer toContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, secondarySlotIndex: toSlotIndex);

            if (fromContainer == toContainer)
                return fromContainer.TryMoveSlot(fromSlotIndex, toSlotIndex);

            if (!fromContainer.TryGetSlot(fromSlotIndex, out InventorySlot fromSlot) || fromSlot.IsEmpty)
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.NoChange, primarySlotIndex: fromSlotIndex);

            int itemId = fromSlot.Stack.ItemId;
            int count = fromSlot.Stack.Count;
            long instanceId = fromSlot.Stack.InstanceId;

            if (!toContainer.TryGetSlot(toSlotIndex, out InventorySlot toSlot))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.InvalidSlotIndex, itemId, secondarySlotIndex: toSlotIndex);

            if (!ItemDatabase.TryGetDefinition(itemId, out ItemDefinition definition))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.DefinitionNotFound, itemId);

            if (!toSlot.IsEmpty)
            {
                if (toSlot.Stack.ItemId != itemId)
                    return TrySwapBetween(fromContainerId, fromSlotIndex, toContainerId, toSlotIndex);

                bool canMergeStacks = definition.IsStackable
                    && instanceId <= 0
                    && toSlot.Stack.InstanceId <= 0;

                if (!canMergeStacks)
                    return TrySwapBetween(fromContainerId, fromSlotIndex, toContainerId, toSlotIndex);
            }

            if (!toContainer.CanAcceptSlot(toSlotIndex, definition))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.SlotRuleDenied, itemId, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex);

            InventoryChangeResult removeResult = fromContainer.TryTakeItemFromSlot(fromSlotIndex, count);
            if (!removeResult.Success)
                return removeResult;

            InventoryChangeResult addResult = instanceId > 0
                ? toContainer.TryAddItemToSlot(toSlotIndex, itemId, count, instanceId)
                : toContainer.TryAddItemToSlot(toSlotIndex, itemId, count);

            if (addResult.Success)
                return addResult.WithSecondaryContainer(toContainerId);

            if (instanceId > 0)
                fromContainer.TryAddItemToSlot(fromSlotIndex, itemId, removeResult.ProcessedCount, instanceId);
            else
                fromContainer.TryAddItemToSlot(fromSlotIndex, itemId, removeResult.ProcessedCount);

            return addResult;
        }

        public InventoryChangeResult TrySwapBetween(
            string containerAId,
            int slotA,
            string containerBId,
            int slotB)
        {
            if (!TryGetContainer(containerAId, out InventoryContainer containerA))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.ContainerNotFound, primarySlotIndex: slotA);

            if (!TryGetContainer(containerBId, out InventoryContainer containerB))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.ContainerNotFound, secondarySlotIndex: slotB);

            if (containerA == containerB)
                return containerA.TrySwapSlots(slotA, slotB);

            if (!containerA.TryGetSlot(slotA, out InventorySlot slotAData) || slotAData.IsEmpty)
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.NoChange, primarySlotIndex: slotA, secondarySlotIndex: slotB);

            if (!containerB.TryGetSlot(slotB, out InventorySlot slotBData))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.InvalidSlotIndex, secondarySlotIndex: slotB);

            if (slotBData.IsEmpty)
                return TryMoveBetween(containerAId, slotA, containerBId, slotB);

            int itemA = slotAData.Stack.ItemId;
            int countA = slotAData.Stack.Count;
            long instanceA = slotAData.Stack.InstanceId;
            int itemB = slotBData.Stack.ItemId;
            int countB = slotBData.Stack.Count;
            long instanceB = slotBData.Stack.InstanceId;

            if (!ItemDatabase.TryGetDefinition(itemA, out ItemDefinition definitionA))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.DefinitionNotFound, itemA, primarySlotIndex: slotA, secondarySlotIndex: slotB);

            if (!ItemDatabase.TryGetDefinition(itemB, out ItemDefinition definitionB))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.DefinitionNotFound, itemB, primarySlotIndex: slotA, secondarySlotIndex: slotB);

            if (!containerB.CanAcceptSlot(slotB, definitionA))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.SlotRuleDenied, itemA, primarySlotIndex: slotA, secondarySlotIndex: slotB);

            if (!containerA.CanAcceptSlot(slotA, definitionB))
                return InventoryChangeResult.Fail(InventoryChangeType.Swap, InventoryFailReason.SlotRuleDenied, itemB, primarySlotIndex: slotA, secondarySlotIndex: slotB);

            using (InventoryGroupTransaction transaction = InventoryGroupTransaction.Begin(this, containerA, containerB))
            {
                InventoryChangeResult removeA = containerA.TryTakeItemFromSlot(slotA, countA);
                if (!removeA.Success)
                    return removeA;

                InventoryChangeResult removeB = containerB.TryTakeItemFromSlot(slotB, countB);
                if (!removeB.Success)
                    return removeB;

                InventoryChangeResult addAtoB = instanceA > 0
                    ? containerB.TryAddItemToSlot(slotB, itemA, countA, instanceA)
                    : containerB.TryAddItemToSlot(slotB, itemA, countA);

                if (!addAtoB.Success)
                    return addAtoB;

                InventoryChangeResult addBtoA = instanceB > 0
                    ? containerA.TryAddItemToSlot(slotA, itemB, countB, instanceB)
                    : containerA.TryAddItemToSlot(slotA, itemB, countB);

                if (!addBtoA.Success)
                    return addBtoA;

                transaction.Commit();

                return InventoryChangeResult.Succeed(
                    InventoryChangeType.Swap,
                    itemA,
                    definitionA,
                    definitionB,
                    countA,
                    countA,
                    0,
                    removeA.TotalItemCountBefore,
                    addAtoB.TotalItemCountAfter,
                    slotA,
                    slotB,
                    addAtoB.ItemWasAcquired,
                    removeA.ItemWasDepleted,
                    containerAId,
                    containerA.Kind,
                    containerBId,
                    MergeIndices(removeA.ChangedSlotIndices, addBtoA.ChangedSlotIndices),
                    MergeSlotChanges(removeA.SlotChanges, MergeSlotChanges(addAtoB.SlotChanges, addBtoA.SlotChanges)));
            }
        }
    }
}
