namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    public static class InventoryEnumPaths
    {
        public const string DefineSymbol = "PJDEV_INVENTORY_ENUMS_GENERATED";
        public const string GeneratedRoot = "Assets/Framework/Runtime/InventorySystem/Generated";
        public const string GeneratedAssemblyName = "PJDev.DevelopKit.Framework.InventorySystem.Generated";
        public const string GeneratedAssemblyGuid = "f8e2a1b0c9d84e6f9a8b7c6d5e4f3a2b";

        public const string RuntimeAssemblyAssetPath =
            "Assets/Framework/Runtime/InventorySystem/PJDev.DevelopKit.Framework.InventorySystem.Runtime.asmdef";

        public const string EditorsAssemblyAssetPath =
            "Assets/Framework/Editor/PJDev.DevelopKit.Framework.Editors.asmdef";

        public const string TestsAssemblyAssetPath =
            "Assets/Framework/Runtime/InventorySystem/Tests/PJDev.DevelopKit.Framework.InventorySystem.Tests.asmdef";

        public static string GeneratedAssemblyAssetPath => $"{GeneratedRoot}/{GeneratedAssemblyName}.asmdef";

        public static string ItemTypeAssetPath => $"{GeneratedRoot}/ItemType.cs";

        public static string ContainerKindAssetPath => $"{GeneratedRoot}/ContainerKind.cs";

        public static string CatalogAssetPath => $"{GeneratedRoot}/InventoryEnumCatalog.Generated.cs";

        public static string RoutesAssetPath => $"{GeneratedRoot}/InventoryEnumRoutes.Generated.cs";
    }
}
