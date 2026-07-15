using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;
using Unity.Collections;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InventoryGroupTransaction : IDisposable
    {
        private readonly List<ContainerSnapshot> snapshots;
        private readonly IItemInstanceStore instanceStore;
        private Dictionary<long, IItemInstanceData> instanceSnapshots;
        private bool isCommitted;
        private bool isRolledBack;
        private bool isDisposed;

        private InventoryGroupTransaction(InventoryGroup group, int capacity)
        {
            instanceStore = group.ItemInstanceStore;
            snapshots = new List<ContainerSnapshot>(Math.Max(0, capacity));
        }

        public static InventoryGroupTransaction Begin(InventoryGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            IReadOnlyList<InventoryContainer> containers = group.Containers;
            var transaction = new InventoryGroupTransaction(group, containers.Count);
            for (int i = 0; i < containers.Count; i++)
                transaction.Capture(containers[i]);

            return transaction;
        }

        internal static InventoryGroupTransaction Begin(
            InventoryGroup group,
            InventoryContainer first,
            InventoryContainer second)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            var transaction = new InventoryGroupTransaction(group, first == second ? 1 : 2);
            transaction.Capture(first);
            transaction.Capture(second);
            return transaction;
        }

        public void Commit() => isCommitted = true;

        public void Rollback()
        {
            if (isCommitted || isRolledBack || isDisposed)
                return;

            RemoveInstancesCreatedDuringTransaction();

            for (int i = 0; i < snapshots.Count; i++)
            {
                ContainerSnapshot snapshot = snapshots[i];
                snapshot.Container.RestoreStateSnapshot(snapshot.Slots);
            }

            RestoreCapturedInstances();
            isRolledBack = true;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            if (!isCommitted && !isRolledBack)
                Rollback();

            for (int i = 0; i < snapshots.Count; i++)
            {
                NativeArray<SlotData> slots = snapshots[i].Slots;
                if (slots.IsCreated)
                    slots.Dispose();
            }

            snapshots.Clear();
            isDisposed = true;
        }

        private void Capture(InventoryContainer container)
        {
            if (container == null)
                return;

            for (int i = 0; i < snapshots.Count; i++)
            {
                if (ReferenceEquals(snapshots[i].Container, container))
                    return;
            }

            NativeArray<SlotData> slots = container.CaptureStateSnapshot();
            snapshots.Add(new ContainerSnapshot(container, slots));
            CaptureInstances(slots);
        }

        private void CaptureInstances(NativeArray<SlotData> slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                long instanceId = slots[i].InstanceId;
                if (instanceId <= 0 || ContainsCapturedInstance(instanceId))
                    continue;

                if (!instanceStore.TryGet(instanceId, out IItemInstanceData data))
                    continue;

                instanceSnapshots ??= new Dictionary<long, IItemInstanceData>();
                instanceSnapshots.Add(instanceId, data);
            }
        }

        private void RemoveInstancesCreatedDuringTransaction()
        {
            for (int snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
            {
                InventoryContainer container = snapshots[snapshotIndex].Container;
                for (int slotIndex = 0; slotIndex < container.SlotCount; slotIndex++)
                {
                    long instanceId = container.GetSlot(slotIndex).Stack.InstanceId;
                    if (instanceId > 0 && !ContainsCapturedInstance(instanceId))
                        instanceStore.Remove(instanceId);
                }
            }
        }

        private void RestoreCapturedInstances()
        {
            if (instanceSnapshots == null)
                return;

            foreach (KeyValuePair<long, IItemInstanceData> snapshot in instanceSnapshots)
                instanceStore.Set(snapshot.Key, snapshot.Value);
        }

        private bool ContainsCapturedInstance(long instanceId) =>
            instanceSnapshots != null && instanceSnapshots.ContainsKey(instanceId);

        private readonly struct ContainerSnapshot
        {
            public ContainerSnapshot(InventoryContainer container, NativeArray<SlotData> slots)
            {
                Container = container;
                Slots = slots;
            }

            public InventoryContainer Container { get; }
            public NativeArray<SlotData> Slots { get; }
        }
    }
}
