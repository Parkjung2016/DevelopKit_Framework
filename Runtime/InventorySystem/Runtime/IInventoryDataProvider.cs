namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// Inventory runtime data entry point.
    /// Implement or compose from SO, CSV, bytes, server payload, etc.
    /// </summary>
    public interface IInventoryDataProvider
    {
        IItemDatabase ItemDatabase { get; }
        IRecipeDatabase RecipeDatabase { get; }
        ILootTableDatabase LootTableDatabase { get; }
    }
}
