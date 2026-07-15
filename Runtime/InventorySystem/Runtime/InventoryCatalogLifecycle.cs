#if !UNITY_6000_5_OR_NEWER
using PJDev.DevelopKit.Framework.Shared.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    internal static class InventoryCatalogLifecycle
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterCleanup() =>
            FrameworkPlayModeCleanup.Register(ClearCatalogs);

        private static void ClearCatalogs()
        {
            ItemCatalog.Clear();
            RecipeCatalog.Clear();
            LootTableCatalog.Clear();
            ItemInstanceCatalog.Clear();
        }
    }
}
#endif