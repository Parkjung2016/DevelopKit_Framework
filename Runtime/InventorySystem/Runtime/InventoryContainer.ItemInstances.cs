using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
        private IItemInstanceStore instanceStore;
        private IItemInstanceFactory instanceFactory;

        public IItemInstanceStore InstanceStore
        {
            get => instanceStore ?? ItemInstanceCatalog.Current;
            set => instanceStore = value;
        }

        public IItemInstanceFactory InstanceFactory
        {
            get => instanceFactory ?? ItemInstanceCatalog.Factory;
            set => instanceFactory = value;
        }

        internal void BindInstanceServices(
            IItemInstanceStore store,
            IItemInstanceFactory factory,
            IItemInstanceIdGenerator idGenerator = null)
        {
            instanceStore = store;
            instanceFactory = factory;
            instanceIdGeneratorOverride = idGenerator;
        }

        private long AllocateInstanceId(int itemId)
        {
            long instanceId = GenerateInstanceId(itemId);
            ItemInstanceRegistrar.Register(InstanceStore, InstanceFactory, itemId, instanceId);
            return instanceId;
        }

        private void EnsureInstanceRegistered(int itemId, long instanceId)
        {
            ItemInstanceRegistrar.Register(InstanceStore, InstanceFactory, itemId, instanceId);
        }

        private void ProcessReleasedInstances()
        {
            if (!changedSlots.IsCreated || !slotSnapshotsBefore.IsCreated)
                return;

            for (int i = 0; i < changedSlots.Length; i++)
            {
                int slotIndex = changedSlots[i];
                if (slotIndex < 0 || slotIndex >= SlotCount)
                    continue;

                SlotData before = slotSnapshotsBefore[i];
                SlotData after = slots[slotIndex];
                if (before.InstanceId > 0 && after.IsEmpty)
                    ItemInstanceRegistrar.Release(InstanceStore, before.InstanceId);
            }
        }
    }
}
