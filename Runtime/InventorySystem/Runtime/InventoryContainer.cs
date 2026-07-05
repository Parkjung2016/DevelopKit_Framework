using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer : IDisposable, IInventoryContainer
    {
        private NativeArray<SlotData> slots;
        private NativeList<SlotData> slotSnapshotsBefore;
        private NativeList<int> changedSlots;
        private readonly IItemDatabase itemDatabase;
        private readonly InventoryContainerDescriptor descriptor;
        private readonly ISlotRule slotRule;
        private readonly IContainerCapacityRule capacityRule;
        private readonly bool usesCustomSlotRule;
        private readonly bool usesItemTypeSlotRule;
        private IItemInstanceIdGenerator instanceIdGenerator = DefaultItemInstanceIdGenerator.Instance;
        private bool isDisposed;
        private int revision;

        public string ContainerId => descriptor.ContainerId;
        public ContainerKind Kind => descriptor.Kind;
        public InventoryContainerDescriptor Descriptor => descriptor;
        public int SlotCount => slots.IsCreated ? slots.Length : 0;
        public int Revision => revision;
        public IItemInstanceIdGenerator InstanceIdGenerator
        {
            get => instanceIdGenerator;
            set => instanceIdGenerator = value ?? DefaultItemInstanceIdGenerator.Instance;
        }
        public NativeArray<SlotData>.ReadOnly SlotDataReadOnly =>
            slots.IsCreated ? slots.AsReadOnly() : default;

        private IItemDatabase Database => ItemCatalog.Resolve(itemDatabase);

        public InventoryContainer(int slotCount, IItemDatabase itemDatabase, InventoryContainerDescriptor descriptor = default)
        {
            if (slotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));

            this.itemDatabase = itemDatabase;
            this.descriptor = string.IsNullOrWhiteSpace(descriptor.ContainerId)
                ? InventoryContainerDescriptor.Main()
                : descriptor;
            slotRule = this.descriptor.SlotRule ?? AnySlotRule.Instance;
            capacityRule = this.descriptor.CapacityRule;
            usesCustomSlotRule = slotRule is not AnySlotRule;
            usesItemTypeSlotRule = slotRule is ItemTypeSlotRule;
            slots = new NativeArray<SlotData>(slotCount, Allocator.Persistent);
            slotSnapshotsBefore = new NativeList<SlotData>(slotCount, Allocator.Persistent);
            changedSlots = new NativeList<int>(slotCount, Allocator.Persistent);
        }

        public NativeArray<SlotData> CaptureStateSnapshot()
        {
            var snapshot = new NativeArray<SlotData>(slots.Length, Allocator.Persistent);
            NativeArray<SlotData>.Copy(slots, snapshot);
            return snapshot;
        }

        public void RestoreStateSnapshot(NativeArray<SlotData> snapshot)
        {
            if (!snapshot.IsCreated || snapshot.Length != slots.Length)
                return;

            NativeArray<SlotData>.Copy(snapshot, slots);
        }

        public bool CanAcceptSlot(int slotIndex, in ItemDefinition definition)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return false;

            if (!usesCustomSlotRule)
                return true;

            if (usesItemTypeSlotRule)
                return slotRule.CanAccept(0, definition);

            return slotRule.CanAccept(slotIndex, definition);
        }

        private bool UsesCustomSlotRule => usesCustomSlotRule;

        #region Add Item

        public InventoryChangeResult TryAddItem(int itemId, int count)
        {
            if (isDisposed)
                return Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (!ItemCatalog.IsReady && itemDatabase == null)
                return Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (itemId <= 0)
                return Fail(InventoryChangeType.Add, InventoryFailReason.InvalidItemId, itemId, count);

            if (count <= 0)
                return Fail(InventoryChangeType.Add, InventoryFailReason.InvalidCount, itemId, count);

            if (!TryGetDefinition(itemId, out ItemDefinition definition))
                return Fail(InventoryChangeType.Add, InventoryFailReason.DefinitionNotFound, itemId, count);

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
            if (isDisposed)
                return Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (!ItemCatalog.IsReady && itemDatabase == null)
                return Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (itemId <= 0)
                return Fail(InventoryChangeType.Add, InventoryFailReason.InvalidItemId, itemId, count);

            if (count <= 0)
                return Fail(InventoryChangeType.Add, InventoryFailReason.InvalidCount, itemId, count);

            if (!TryGetDefinition(itemId, out ItemDefinition definition))
                return Fail(InventoryChangeType.Add, InventoryFailReason.DefinitionNotFound, itemId, count);

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

        public InventoryChangeResult TryRemoveItemFromSlot(int slotIndex, int count)
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

        #region Query

        public bool HasItem(int itemId, int count = 1) =>
            !isDisposed && InventoryBurstOperations.HasItem(ref slots, itemId, count);

        public int GetItemCount(int itemId) =>
            isDisposed ? 0 : InventoryBurstOperations.CountItem(ref slots, itemId);

        public JobHandle ScheduleGetItemCount(int itemId, NativeReference<int> result, JobHandle dependsOn = default)
        {
            var job = new CountItemJob
            {
                Slots = slots,
                ItemId = itemId,
                Result = result
            };
            return job.Schedule(dependsOn);
        }

        public JobHandle ScheduleHasItem(int itemId, int count, NativeReference<bool> result, JobHandle dependsOn = default)
        {
            var job = new HasItemJob
            {
                Slots = slots,
                ItemId = itemId,
                RequiredCount = count,
                Result = result
            };
            return job.Schedule(dependsOn);
        }

        public void GetOccupiedSlotIndices(List<int> results)
        {
            results.Clear();
            if (isDisposed)
                return;

            int slotCount = slots.Length;
            if (results.Capacity < slotCount)
                results.Capacity = slotCount;

            for (int i = 0; i < slotCount; i++)
            {
                if (!slots[i].IsEmpty)
                    results.Add(i);
            }
        }

        public InventorySlotSaveData[] ExportOccupiedSlots()
        {
            if (isDisposed)
                return Array.Empty<InventorySlotSaveData>();

            int slotCount = slots.Length;
            if (slotCount == 0)
                return Array.Empty<InventorySlotSaveData>();

            int occupiedCount = 0;
            for (int i = 0; i < slotCount; i++)
            {
                if (!slots[i].IsEmpty)
                    occupiedCount++;
            }

            if (occupiedCount == 0)
                return Array.Empty<InventorySlotSaveData>();

            var result = new InventorySlotSaveData[occupiedCount];
            int writeIndex = 0;

            for (int i = 0; i < slotCount; i++)
            {
                SlotData slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                result[writeIndex++] = new InventorySlotSaveData
                {
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Count = slot.Count,
                    InstanceId = slot.InstanceId
                };
            }

            return result;
        }

        public InventorySlot GetSlot(int slotIndex) =>
            TryGetSlot(slotIndex, out InventorySlot slot) ? slot : default;

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (isDisposed || slotIndex < 0 || slotIndex >= SlotCount)
            {
                slot = default;
                return false;
            }

            slot = InventorySlot.From(slots[slotIndex]);
            return true;
        }

        public bool IsSlotEmpty(int slotIndex) =>
            !isDisposed && slotIndex >= 0 && slotIndex < SlotCount && slots[slotIndex].IsEmpty;

        #endregion

        public void Dispose()
        {
            if (isDisposed)
                return;

            if (changedSlots.IsCreated)
                changedSlots.Dispose();

            if (slotSnapshotsBefore.IsCreated)
                slotSnapshotsBefore.Dispose();

            if (slots.IsCreated)
                slots.Dispose();

            isDisposed = true;
        }

        private bool TryGetDefinition(int itemId, out ItemDefinition definition) =>
            Database.TryGetDefinition(itemId, out definition);

        private ItemDefinition ResolveDefinition(int itemId)
        {
            if (itemId <= 0)
                return default;

            return Database.TryGetDefinition(itemId, out ItemDefinition definition) ? definition : default;
        }

        private int GetSlotItemId(int slotIndex) =>
            slotIndex >= 0 && slotIndex < SlotCount ? slots[slotIndex].ItemId : 0;

        private int GetSnapshotItemId(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || !changedSlots.IsCreated)
                return 0;

            for (int i = 0; i < changedSlots.Length; i++)
            {
                if (changedSlots[i] == slotIndex)
                    return slotSnapshotsBefore[i].ItemId;
            }

            return 0;
        }

        private InventoryChangeResult Fail(
            InventoryChangeType changeType,
            InventoryFailReason reason,
            int itemId = 0,
            int requestedCount = 0,
            int totalItemCountBefore = 0,
            int primarySlotIndex = -1,
            int secondarySlotIndex = -1) =>
            InventoryChangeResult.Fail(
                changeType,
                reason,
                itemId,
                requestedCount,
                primarySlotIndex,
                secondarySlotIndex,
                totalItemCountBefore,
                ResolveDefinition(itemId),
                ResolveDefinition(GetSlotItemId(secondarySlotIndex)),
                descriptor.ContainerId,
                descriptor.Kind);

        private InventoryChangeResult CreateSuccess(
            InventoryChangeType changeType,
            int itemId,
            int requestedCount,
            int processedCount,
            int remainder,
            int totalItemCountBefore,
            bool itemWasAcquired,
            bool itemWasDepleted,
            int primarySlotIndex = -1,
            int secondarySlotIndex = -1,
            ItemDefinition knownDefinition = default)
        {
            int totalItemCountAfter = ResolveTotalItemCountAfter(
                changeType,
                itemId,
                processedCount,
                totalItemCountBefore);
            ItemDefinition definition = knownDefinition.ItemId > 0 ? knownDefinition : ResolveDefinition(itemId);
            int secondaryItemId = secondarySlotIndex >= 0 ? GetSnapshotItemId(secondarySlotIndex) : 0;
            BuildChangeResultArrays(out int[] changedSlotIndices, out InventorySlotChange[] slotChanges);
            CommitChange();

            return InventoryChangeResult.Succeed(
                changeType,
                itemId,
                definition,
                secondaryItemId > 0 ? ResolveDefinition(secondaryItemId) : default,
                requestedCount,
                processedCount,
                remainder,
                totalItemCountBefore,
                totalItemCountAfter,
                primarySlotIndex,
                secondarySlotIndex,
                itemWasAcquired,
                itemWasDepleted,
                descriptor.ContainerId,
                descriptor.Kind,
                null,
                changedSlotIndices,
                slotChanges);
        }

        private void CommitChange() => revision++;

        private int ResolveTotalItemCountAfter(
            InventoryChangeType changeType,
            int itemId,
            int processedCount,
            int totalItemCountBefore)
        {
            if (itemId <= 0)
                return totalItemCountBefore;

            return changeType switch
            {
                InventoryChangeType.Add => totalItemCountBefore + processedCount,
                InventoryChangeType.Remove => totalItemCountBefore - processedCount,
                InventoryChangeType.Clear => totalItemCountBefore - processedCount,
                InventoryChangeType.Move => totalItemCountBefore,
                _ => GetItemCount(itemId)
            };
        }

        private void BuildChangeResultArrays(out int[] changedSlotIndices, out InventorySlotChange[] slotChanges)
        {
            int length = changedSlots.IsCreated ? changedSlots.Length : 0;
            if (length == 0)
            {
                changedSlotIndices = Array.Empty<int>();
                slotChanges = Array.Empty<InventorySlotChange>();
                return;
            }

            changedSlotIndices = new int[length];
            slotChanges = new InventorySlotChange[length];

            for (int i = 0; i < length; i++)
            {
                int index = changedSlots[i];
                changedSlotIndices[i] = index;
                slotChanges[i] = InventorySlotChange.From(index, slotSnapshotsBefore[i], slots[index]);
            }
        }
    }
}
