using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InMemoryRecipeDatabase : IRecipeDatabase
    {
        private readonly Dictionary<string, RecipeDefinition> recipes = new();

        public void Clear() => recipes.Clear();

        public void Register(in RecipeDefinition recipe)
        {
            if (string.IsNullOrWhiteSpace(recipe.RecipeId))
                return;

            recipes[recipe.RecipeId] = recipe;
        }

        public void RegisterRange(IEnumerable<RecipeDefinition> source)
        {
            if (source == null)
                return;

            foreach (RecipeDefinition recipe in source)
                Register(recipe);
        }

        public bool TryGetRecipe(string recipeId, out RecipeDefinition recipe) =>
            recipes.TryGetValue(recipeId, out recipe);

        public void FindCraftable(InventoryGroup group, List<RecipeDefinition> results)
        {
            results.Clear();
            if (group == null)
                return;

            foreach (KeyValuePair<string, RecipeDefinition> pair in recipes)
            {
                RecipeDefinition recipe = pair.Value;
                if (InventoryCrafting.CanCraft(group, recipe.Costs, recipe.Rewards, out _))
                    results.Add(recipe);
            }
        }
    }
}
