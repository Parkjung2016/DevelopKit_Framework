using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;
using Unity.Collections;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InventoryGroupTransaction : IDisposable
    {
        private readonly InventoryGroup group;
        private readonly Dictionary<string, NativeArray<SlotData>> snapshots = new();
        private bool isCommitted;
        private bool isDisposed;

        private InventoryGroupTransaction(InventoryGroup group)
        {
            this.group = group;
        }

        public static InventoryGroupTransaction Begin(InventoryGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            var transaction = new InventoryGroupTransaction(group);
            IReadOnlyList<InventoryContainer> containers = group.Containers;
            for (int i = 0; i < containers.Count; i++)
            {
                InventoryContainer container = containers[i];
                transaction.snapshots.Add(container.ContainerId, container.CaptureStateSnapshot());
            }

            return transaction;
        }

        public void Commit() => isCommitted = true;

        public void Rollback()
        {
            if (isCommitted || isDisposed)
                return;

            foreach (KeyValuePair<string, NativeArray<SlotData>> pair in snapshots)
            {
                if (!group.TryGetContainer(pair.Key, out InventoryContainer container))
                    continue;

                container.RestoreStateSnapshot(pair.Value);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            if (!isCommitted)
                Rollback();

            foreach (NativeArray<SlotData> snapshot in snapshots.Values)
            {
                if (snapshot.IsCreated)
                    snapshot.Dispose();
            }

            snapshots.Clear();
            isDisposed = true;
        }
    }
}
