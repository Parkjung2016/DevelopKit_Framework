#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class LootTableCatalog
    {
        private static ILootTableDatabase current;

        public static bool IsReady => current != null;

        public static ILootTableDatabase Current => current ?? NullLootTableDatabase.Instance;

        public static void Set(ILootTableDatabase database)
        {
            if (database == null)
                throw new System.ArgumentNullException(nameof(database));

            current = database;
        }

        public static void Clear() => current = null;

        public static ILootTableDatabase Resolve(ILootTableDatabase database = null) =>
            database ?? Current;
    }
}
