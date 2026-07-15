using System.Collections.Generic;
using PJDev.DevelopKit.Framework.RandomSystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryGroup
    {
        public bool CanCraft(
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason) =>
            InventoryCrafting.CanCraft(this, costs, rewards, out reason);

        public InventoryChangeResult TryCraft(
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards) =>
            InventoryCrafting.TryCraft(this, costs, rewards);

        public bool CanCraft(string recipeId, out InventoryFailReason reason)
        {
            if (!RecipeDatabase.TryGetRecipe(recipeId, out RecipeDefinition recipe))
                return FailCraftLookup(out reason, InventoryFailReason.RecipeNotFound);

            return CanCraft(recipe.Costs, recipe.Rewards, out reason);
        }

        public bool CanCraft(in RecipeDefinition recipe, out InventoryFailReason reason) =>
            InventoryCrafting.CanCraft(this, recipe, out reason);

        public InventoryChangeResult TryCraft(string recipeId)
        {
            if (!RecipeDatabase.TryGetRecipe(recipeId, out RecipeDefinition recipe))
                return InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.RecipeNotFound);

            return TryCraft(recipe);
        }

        public InventoryChangeResult TryCraft(in RecipeDefinition recipe) =>
            InventoryCrafting.TryCraft(this, recipe);

        public InventoryChangeResult TryGrantLoot(string tableId, IRandomSource random = null)
        {
            if (!LootTableDatabase.TryGetTable(tableId, out LootTableDefinition table))
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.LootTableNotFound);

            return LootRoller.TryGrantLoot(this, table, random);
        }

        public InventoryChangeResult TryGrantLoot(in LootTableDefinition table, IRandomSource random = null) =>
            LootRoller.TryGrantLoot(this, table, random);

        public InventoryGroupSaveData ExportSaveData() => InventorySerializer.Export(this);

        public InventoryGroupDelta ComputeDelta(InventoryGroupSaveData baseline) =>
            InventoryDeltaComputer.ComputeGroupDelta(this, baseline);

        public InventoryGroupTransaction BeginTransaction() => InventoryGroupTransaction.Begin(this);

        private static bool FailCraftLookup(out InventoryFailReason reason, InventoryFailReason value)
        {
            reason = value;
            return false;
        }
    }
}
