using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.Core.Runtime
{
    /// <summary>전역 Catalog 등록·해제.</summary>
    public static partial class FrameworkGlobals
    {
        public static void RegisterAll(FrameworkDatabaseSetupSO setup) => setup?.RegisterAll();

        public static void RegisterDatabases(InventoryDatabaseSetupSO inventoryDatabases)
        {
            inventoryDatabases?.RegisterGlobals();
        }

        public static void RegisterStatDatabase(StatDatabaseSO statDatabase)
        {
            if (statDatabase != null)
                StatCatalog.Set(statDatabase);
        }

        public static void RegisterStatDatabase(IStatCatalog statCatalog)
        {
            if (statCatalog != null)
                StatCatalog.Set(statCatalog);
        }

        public static void ClearCatalogs()
        {
            ItemCatalog.Clear();
            RecipeCatalog.Clear();
            LootTableCatalog.Clear();
            ItemInstanceCatalog.Clear();
            StatCatalog.Clear();
        }
    }
}
