#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class RecipeCatalog
    {
        private static IRecipeDatabase current;

        public static bool IsReady => current != null;

        public static IRecipeDatabase Current => current ?? NullRecipeDatabase.Instance;

        public static void Set(IRecipeDatabase database)
        {
            if (database == null)
                throw new System.ArgumentNullException(nameof(database));

            current = database;
        }

        public static void Clear() => current = null;

        public static IRecipeDatabase Resolve(IRecipeDatabase database = null) =>
            database ?? Current;
    }
}
