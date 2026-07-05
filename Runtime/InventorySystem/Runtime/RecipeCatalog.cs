#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif
using PJDev.DevelopKit.Framework.Shared.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class RecipeCatalog
    {
        public static bool IsReady => GlobalRegistry<IRecipeDatabase>.IsReady;

        public static IRecipeDatabase Current =>
            GlobalRegistry<IRecipeDatabase>.ResolveOrDefault(null, NullRecipeDatabase.Instance);

        public static void Set(IRecipeDatabase database) => GlobalRegistry<IRecipeDatabase>.Set(database);

        public static void Clear() => GlobalRegistry<IRecipeDatabase>.Clear();

        public static IRecipeDatabase Resolve(IRecipeDatabase database = null) =>
            database ?? Current;
    }
}
