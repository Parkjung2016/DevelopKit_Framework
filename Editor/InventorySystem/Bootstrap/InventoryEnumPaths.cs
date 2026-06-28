using System.IO;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    public static class InventoryEnumPaths
    {
        public const string PackageGeneratedRoot = "Assets/PJDev/Inventory/Generated";

        private const string LegacyGeneratedAssemblyFileName = "PJDev.DevelopKit.Framework.InventorySystem.Generated.asmdef";

        private const string RuntimeAssemblyName = "PJDev.DevelopKit.Framework.InventorySystem.Runtime";

        public static string RuntimeRoot
        {
            get
            {
                string[] guids = AssetDatabase.FindAssets($"{RuntimeAssemblyName} t:asmdef");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.EndsWith(".asmdef", System.StringComparison.OrdinalIgnoreCase))
                        return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? PackageGeneratedRoot;
                }

                return "Assets/Framework/Runtime/InventorySystem/Runtime";
            }
        }

        public static string GeneratedRoot
        {
            get
            {
                string runtimeRoot = RuntimeRoot;
                if (IsPackagePath(runtimeRoot))
                    return PackageGeneratedRoot;

                return $"{runtimeRoot}/Generated";
            }
        }

        public static string LegacyGeneratedAssemblyAssetPath => $"{GeneratedRoot}/{LegacyGeneratedAssemblyFileName}";

        public static string ItemTypeAssetPath => $"{GeneratedRoot}/ItemType.cs";

        public static string ContainerKindAssetPath => $"{GeneratedRoot}/ContainerKind.cs";

        public static string CatalogAssetPath => $"{GeneratedRoot}/InventoryEnumCatalog.Generated.cs";

        public static string RoutesAssetPath => $"{GeneratedRoot}/InventoryEnumRoutes.Generated.cs";

        public static bool ContainerKindExists()
        {
            string fullPath = Path.GetFullPath(ContainerKindAssetPath);
            return File.Exists(fullPath);
        }

        private static bool IsPackagePath(string assetPath) =>
            assetPath.Contains("/PackageCache/", System.StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase);
    }
}
