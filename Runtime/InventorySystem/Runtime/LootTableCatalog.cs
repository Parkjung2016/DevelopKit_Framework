#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif
using PJDev.DevelopKit.Framework.Shared.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class LootTableCatalog
    {
        public static bool IsReady => GlobalRegistry<ILootTableDatabase>.IsReady;

        public static ILootTableDatabase Current =>
            GlobalRegistry<ILootTableDatabase>.ResolveOrDefault(null, NullLootTableDatabase.Instance);

        public static void Set(ILootTableDatabase database) => GlobalRegistry<ILootTableDatabase>.Set(database);

        public static void Clear() => GlobalRegistry<ILootTableDatabase>.Clear();

        public static ILootTableDatabase Resolve(ILootTableDatabase database = null) =>
            database ?? Current;
    }
}
