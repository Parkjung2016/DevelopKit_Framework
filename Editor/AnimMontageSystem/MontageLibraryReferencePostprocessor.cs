using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageLibraryReferencePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (deletedAssets == null || deletedAssets.Length == 0)
                return;

            MontageLibraryReferenceCleaner.RemoveMissingMontageReferences();
        }
    }
}
