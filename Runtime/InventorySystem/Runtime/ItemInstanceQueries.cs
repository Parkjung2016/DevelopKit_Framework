namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class ItemInstanceQueries
    {
        public static bool TryGet<T>(
            IItemInstanceStore store,
            long instanceId,
            out T data) where T : class, IItemInstanceData
        {
            data = null;
            return store != null && instanceId > 0 && store.TryGet(instanceId, out data);
        }

        /// <summary>Store에 없으면 <paramref name="itemId"/>로 Factory fallback을 시도합니다.</summary>
        public static bool TryGet(
            IItemInstanceStore store,
            long instanceId,
            int itemId,
            out IItemInstanceData data)
        {
            data = null;
            if (instanceId <= 0)
                return false;

            if (store != null && store.TryGet(instanceId, out IItemInstanceData stored))
            {
                data = stored;
                return true;
            }

            IItemInstanceFactory factory = ItemInstanceCatalog.Factory;
            if (factory == null || !factory.TryCreate(itemId, out data))
                return false;

            ItemInstanceData.BindIfSupported(data, itemId, instanceId);
            store?.Set(instanceId, data);
            return true;
        }

        public static T GetOrNull<T>(IItemInstanceStore store, long instanceId) where T : class, IItemInstanceData
        {
            TryGet(store, instanceId, out T data);
            return data;
        }
    }
}
