using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_RecipeDatabase", menuName = "SO/InventorySystem/RecipeDatabase")]
    public class RecipeDatabaseSO : ScriptableObject, IRecipeDatabase
    {
        [field: SerializeField] public RecipeSO[] Recipes { get; set; } = System.Array.Empty<RecipeSO>();

        private readonly Dictionary<string, RecipeSO> recipesById = new();
        private readonly Dictionary<string, RecipeDefinition> definitionCache = new();

        private void OnEnable() => RebuildCache();

        private void OnValidate() => RebuildCache();

        public void RebuildCache()
        {
            recipesById.Clear();
            definitionCache.Clear();

            if (Recipes == null)
                return;

            for (int i = 0; i < Recipes.Length; i++)
            {
                RecipeSO recipe = Recipes[i];
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.RecipeId))
                    continue;

                if (recipesById.ContainsKey(recipe.RecipeId))
                {
                    CDebug.LogWarning($"RecipeDatabaseSO : duplicate recipe id {recipe.RecipeId} in {name}.");
                    continue;
                }

                recipesById.Add(recipe.RecipeId, recipe);
                definitionCache.Add(recipe.RecipeId, recipe.ToDefinition());
            }
        }

        public bool TryGetRecipe(string recipeId, out RecipeDefinition recipe) =>
            definitionCache.TryGetValue(recipeId, out recipe);

        public bool TryGetRecipeAsset(string recipeId, out RecipeSO recipe) =>
            recipesById.TryGetValue(recipeId, out recipe);

        public void FindCraftable(InventoryGroup group, List<RecipeDefinition> results)
        {
            results.Clear();
            if (Recipes == null || group == null)
                return;

            for (int i = 0; i < Recipes.Length; i++)
            {
                RecipeSO recipe = Recipes[i];
                if (recipe == null)
                    continue;

                RecipeDefinition definition = recipe.ToDefinition();
                if (InventoryCrafting.CanCraft(group, definition.Costs, definition.Rewards, out _))
                    results.Add(definition);
            }
        }
    }
}
