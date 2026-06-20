namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InventoryDataProvider : IInventoryDataProvider
    {
        public IItemDatabase ItemDatabase { get; }
        public IRecipeDatabase RecipeDatabase { get; }
        public ILootTableDatabase LootTableDatabase { get; }

        public InventoryDataProvider(
            IItemDatabase itemDatabase,
            IRecipeDatabase recipeDatabase = null,
            ILootTableDatabase lootTableDatabase = null)
        {
            ItemDatabase = itemDatabase;
            RecipeDatabase = recipeDatabase ?? NullRecipeDatabase.Instance;
            LootTableDatabase = lootTableDatabase ?? NullLootTableDatabase.Instance;
        }

        public static InventoryDataProvider FromItems(IItemDatabase itemDatabase) =>
            new(itemDatabase);

        public static InventoryDataProvider FromCatalog(
            IItemCatalog itemCatalog,
            IRecipeDatabase recipeDatabase = null,
            ILootTableDatabase lootTableDatabase = null) =>
            new(itemCatalog, recipeDatabase, lootTableDatabase);
    }
}
