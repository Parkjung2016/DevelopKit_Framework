#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// <see cref="IItemInstanceStore"/> 전역 접근점입니다. <see cref="ItemCatalog"/>와 같이 어디서든 인스턴스 데이터를 조회할 수 있습니다.
    /// </summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class ItemInstanceCatalog
    {
        private static IItemInstanceStore current;
        private static IItemInstanceFactory factory;

        public static bool IsReady => current != null;

        public static IItemInstanceStore Current => current ?? NullItemInstanceStore.Instance;

        public static IItemInstanceFactory Factory => factory ?? NullItemInstanceFactory.Instance;

        public static void Configure(IItemInstanceStore store, IItemInstanceFactory instanceFactory = null)
        {
            current = store ?? throw new System.ArgumentNullException(nameof(store));
            factory = instanceFactory;
        }

        public static void Set(IItemInstanceStore store) => Configure(store);

        public static void SetFactory(IItemInstanceFactory instanceFactory) =>
            factory = instanceFactory ?? throw new System.ArgumentNullException(nameof(instanceFactory));

        public static void Clear()
        {
            current = null;
            factory = null;
        }

        public static IItemInstanceStore Resolve(IItemInstanceStore store = null) =>
            store ?? Current;

        public static bool TryGet<T>(long instanceId, out T data) where T : class, IItemInstanceData =>
            Current.TryGet(instanceId, out data);

        public static bool TryGet(long instanceId, int itemId, out IItemInstanceData data) =>
            ItemInstanceQueries.TryGet(Current, instanceId, itemId, out data);
    }

    internal sealed class NullItemInstanceStore : IItemInstanceStore
    {
        public static readonly NullItemInstanceStore Instance = new();

        public bool TryGet<T>(long instanceId, out T data) where T : class, IItemInstanceData
        {
            data = null;
            return false;
        }

        public void Set<T>(long instanceId, T data) where T : class, IItemInstanceData
        {
        }

        public bool Remove(long instanceId) => false;

        public bool Contains(long instanceId) => false;
    }
}
