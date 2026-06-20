using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IRecipeDatabase
    {
        bool TryGetRecipe(string recipeId, out RecipeDefinition recipe);
        void FindCraftable(InventoryGroup group, List<RecipeDefinition> results);
    }
}
