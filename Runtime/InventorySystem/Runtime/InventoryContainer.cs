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
        private IItemInstanceIdGenerator instanceIdGeneratorOverride;
        private bool isDisposed;
        private int revision;

        public string ContainerId => descriptor.ContainerId;
        public ContainerKind Kind => descriptor.Kind;
        public InventoryContainerDescriptor Descriptor => descriptor;
        public int SlotCount => slots.IsCreated ? slots.Length : 0;
        public int Revision => revision;
        public IItemInstanceIdGenerator InstanceIdGenerator
        {
            get => ItemInstanceCatalog.ResolveIdGenerator(instanceIdGeneratorOverride);
            set => instanceIdGeneratorOverride = value;
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
