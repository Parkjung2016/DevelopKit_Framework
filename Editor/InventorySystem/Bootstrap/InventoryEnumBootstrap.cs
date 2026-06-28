using System.IO;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    [InitializeOnLoad]
    internal static class InventoryEnumBootstrap
    {
        static InventoryEnumBootstrap()
        {
            EditorApplication.delayCall += EnsureGeneratedEnumsExist;
        }

        private static void EnsureGeneratedEnumsExist()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            bool missing = !InventoryEnumPaths.ContainerKindExists();
            bool hadLegacyAssembly = File.Exists(Path.GetFullPath(InventoryEnumPaths.LegacyGeneratedAssemblyAssetPath));

            if (!missing && !hadLegacyAssembly)
                return;

            InventoryEnumDefaultWriter.WriteAllIfMissing();
            AssetDatabase.Refresh();
        }
    }
}
