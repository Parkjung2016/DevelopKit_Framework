using System;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;
using Unity.Collections;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InventoryTransaction : IDisposable
    {
        private readonly InventoryContainer container;
        private NativeArray<SlotData> snapshot;
        private bool isCommitted;
        private bool isDisposed;

        private InventoryTransaction(InventoryContainer container, NativeArray<SlotData> snapshot)
        {
            this.container = container;
            this.snapshot = snapshot;
        }

        public static InventoryTransaction Begin(InventoryContainer container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            NativeArray<SlotData> snapshot = container.CaptureStateSnapshot();
            return new InventoryTransaction(container, snapshot);
        }

        public void Commit() => isCommitted = true;

        public void Rollback()
        {
            if (isCommitted || isDisposed)
                return;

            container.RestoreStateSnapshot(snapshot);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            if (!isCommitted)
                Rollback();

            if (snapshot.IsCreated)
                snapshot.Dispose();

            isDisposed = true;
        }
    }
}
