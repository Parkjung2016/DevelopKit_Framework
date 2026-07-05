#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// ItemInstance Store / Factory / IdGenerator 전역 접근점입니다.
    /// <see cref="ItemCatalog"/>와 같이 어디서든 인스턴스 데이터·ID를 사용할 수 있습니다.
    /// </summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class ItemInstanceCatalog
    {
        private static IItemInstanceStore current;
        private static IItemInstanceFactory factory;
        private static IItemInstanceIdGenerator idGenerator;

        public static bool IsReady => current != null;

        public static IItemInstanceStore Current => current ?? NullItemInstanceStore.Instance;

        public static IItemInstanceFactory Factory => factory ?? NullItemInstanceFactory.Instance;

        public static IItemInstanceIdGenerator IdGenerator =>
            idGenerator ?? SnowflakeItemInstanceIdGenerator.Instance;

        public static void Configure(
            IItemInstanceStore store,
            IItemInstanceFactory instanceFactory = null,
            IItemInstanceIdGenerator instanceIdGenerator = null)
        {
            current = store ?? throw new System.ArgumentNullException(nameof(store));
            factory = instanceFactory;
            idGenerator = instanceIdGenerator;
        }

        public static void Set(IItemInstanceStore store) => Configure(store);

        public static void SetFactory(IItemInstanceFactory instanceFactory) =>
            factory = instanceFactory ?? throw new System.ArgumentNullException(nameof(instanceFactory));

        public static void SetIdGenerator(IItemInstanceIdGenerator instanceIdGenerator) =>
            idGenerator = instanceIdGenerator ?? throw new System.ArgumentNullException(nameof(instanceIdGenerator));

        public static void Clear()
        {
            current = null;
            factory = null;
            idGenerator = null;
        }

        public static IItemInstanceStore Resolve(IItemInstanceStore store = null) =>
            store ?? Current;

        public static IItemInstanceIdGenerator ResolveIdGenerator(IItemInstanceIdGenerator generator = null) =>
            generator ?? IdGenerator;

        /// <summary>고유 InstanceId를 생성합니다. 인벤 슬롯 없이 ID만 필요할 때 사용합니다.</summary>
        public static long Generate(int itemId) => IdGenerator.Generate(itemId);

        /// <summary>InstanceId 생성 + Factory payload 생성 + Store 등록을 한 번에 수행합니다. <see cref="IsReady"/>가 true일 때만 Store에 등록합니다.</summary>
        public static bool TryCreateRegistered(int itemId, out long instanceId, out IItemInstanceData data)
        {
            instanceId = Generate(itemId);
            if (!IsReady)
            {
                data = null;
                return false;
            }

            if (!Factory.TryCreate(itemId, out data))
            {
                instanceId = 0;
                return false;
            }

            ItemInstanceData.BindIfSupported(data, itemId, instanceId);
            Current.Set(instanceId, data);
            return true;
        }

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
