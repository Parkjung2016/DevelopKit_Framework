using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct RecipeDefinition
    {
        public string RecipeId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<InventoryRecipeEntry> Costs { get; }
        public IReadOnlyList<InventoryRecipeEntry> Rewards { get; }

        public RecipeDefinition(
            string recipeId,
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            string displayName = null)
        {
            RecipeId = recipeId;
            DisplayName = displayName;
            Costs = costs ?? Array.Empty<InventoryRecipeEntry>();
            Rewards = rewards ?? Array.Empty<InventoryRecipeEntry>();
        }

        public static RecipeDefinition Create(
            string recipeId,
            InventoryRecipeEntry[] costs,
            InventoryRecipeEntry[] rewards,
            string displayName = null) =>
            new(recipeId, costs, rewards, displayName);
    }
}
