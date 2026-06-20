namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class ScriptableInventoryDataProvider : IInventoryDataProvider
    {
        public IItemDatabase ItemDatabase { get; }
        public IRecipeDatabase RecipeDatabase { get; }
        public ILootTableDatabase LootTableDatabase { get; }

        public ScriptableInventoryDataProvider(InventorySetupSO setup)
        {
            ItemDatabase = setup?.ItemDatabase;
            RecipeDatabase = setup?.RecipeDatabase != null
                ? setup.RecipeDatabase
                : NullRecipeDatabase.Instance;
            LootTableDatabase = setup?.LootTableDatabase != null
                ? setup.LootTableDatabase
                : NullLootTableDatabase.Instance;
        }

        public ScriptableInventoryDataProvider(
            ItemDatabaseSO itemDatabase,
            RecipeDatabaseSO recipeDatabase = null,
            LootTableDatabaseSO lootTableDatabase = null)
        {
            ItemDatabase = itemDatabase;
            RecipeDatabase = recipeDatabase != null
                ? recipeDatabase
                : NullRecipeDatabase.Instance;
            LootTableDatabase = lootTableDatabase != null
                ? lootTableDatabase
                : NullLootTableDatabase.Instance;
        }
    }
}
