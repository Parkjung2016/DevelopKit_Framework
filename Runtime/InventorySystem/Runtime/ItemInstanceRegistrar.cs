namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    internal static class ItemInstanceRegistrar
    {
        public static void Register(
            IItemInstanceStore store,
            IItemInstanceFactory factory,
            int itemId,
            long instanceId)
        {
            if (store == null || instanceId <= 0 || store.Contains(instanceId))
                return;

            if (factory != null && factory.TryCreate(itemId, out IItemInstanceData data))
                store.Set(instanceId, data);
        }

        public static void Release(IItemInstanceStore store, long instanceId)
        {
            if (store == null || instanceId <= 0)
                return;

            store.Remove(instanceId);
        }
    }
}
