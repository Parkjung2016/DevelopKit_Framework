using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class NullRecipeDatabase : IRecipeDatabase
    {
        public static readonly NullRecipeDatabase Instance = new();

        private NullRecipeDatabase() { }

        public bool TryGetRecipe(string recipeId, out RecipeDefinition recipe)
        {
            recipe = default;
            return false;
        }

        public void FindCraftable(InventoryGroup group, List<RecipeDefinition> results) =>
            results.Clear();
    }
}
