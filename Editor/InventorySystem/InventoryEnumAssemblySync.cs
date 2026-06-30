using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    [InitializeOnLoad]
    internal static class InventoryEnumAssemblySync
    {
        static InventoryEnumAssemblySync()
        {
            EditorApplication.delayCall += Sync;
        }

        private static void Sync()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!InventoryEnumAssemblyConfigurator.SyncGeneratedMode())
                return;

            AssetDatabase.Refresh();
        }
    }
}
