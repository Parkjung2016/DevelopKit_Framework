using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_InventoryDatabaseSetup", menuName = "PJDev/InventorySystem/Database Setup")]
    public class InventoryDatabaseSetupSO : ScriptableObject
    {
        [field: SerializeField] public ItemDatabaseSO ItemDatabase { get; set; }
        [field: SerializeField] public RecipeDatabaseSO RecipeDatabase { get; set; }
        [field: SerializeField] public LootTableDatabaseSO LootTableDatabase { get; set; }

        public void RegisterGlobals()
        {
            if (ItemDatabase != null)
                ItemCatalog.Set(ItemDatabase);

            if (RecipeDatabase != null)
                RecipeCatalog.Set(RecipeDatabase);

            if (LootTableDatabase != null)
                LootTableCatalog.Set(LootTableDatabase);
        }
    }
}
