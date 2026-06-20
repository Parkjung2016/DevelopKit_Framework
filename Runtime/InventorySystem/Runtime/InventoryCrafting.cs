using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventoryCrafting
    {
        public static bool CanCraft(
            InventoryGroup group,
            RecipeSO recipe,
            out InventoryFailReason reason) =>
            recipe == null
                ? Fail(out reason, InventoryFailReason.InvalidCount)
                : CanCraft(group, recipe.Costs, recipe.Rewards, out reason);

        public static bool CanCraft(
            InventoryGroup group,
            in RecipeDefinition recipe,
            out InventoryFailReason reason) =>
            CanCraft(group, recipe.Costs, recipe.Rewards, out reason);

        public static InventoryChangeResult TryCraft(InventoryGroup group, RecipeSO recipe) =>
            recipe == null
                ? InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.InvalidCount)
                : TryCraft(group, recipe.Costs, recipe.Rewards);

        public static InventoryChangeResult TryCraft(InventoryGroup group, in RecipeDefinition recipe) =>
            TryCraft(group, recipe.Costs, recipe.Rewards);

        public static bool CanCraft(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason)
        {
            reason = InventoryFailReason.None;
            if (group == null)
                return Fail(out reason, InventoryFailReason.DatabaseNotReady);

            if (costs == null || costs.Count == 0)
                return Fail(out reason, InventoryFailReason.InvalidCount);

            for (int i = 0; i < costs.Count; i++)
            {
                InventoryRecipeEntry cost = costs[i];
                if (!group.HasItem(cost.ItemId, cost.Count))
                    return Fail(out reason, InventoryFailReason.NoChange);
            }

            if (rewards == null || rewards.Count == 0)
                return true;

            return CanCraftWithSimulatedTransaction(group, costs, rewards, out reason);
        }

        public static InventoryChangeResult TryCraft(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards)
        {
            if (!CanCraft(group, costs, rewards, out InventoryFailReason reason))
                return InventoryChangeResult.Fail(InventoryChangeType.Craft, reason);

            using InventoryGroupTransaction transaction = InventoryGroupTransaction.Begin(group);

            for (int i = 0; i < costs.Count; i++)
            {
                InventoryRecipeEntry cost = costs[i];
                InventoryChangeResult removeResult = group.TryRemoveItem(cost.ItemId, cost.Count);
                if (!removeResult.Success || removeResult.Remainder > 0)
                    return InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.NoChange, cost.ItemId, cost.Count);
            }

            InventoryChangeResult lastResult = InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.NoChange);
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    InventoryRecipeEntry reward = rewards[i];
                    lastResult = group.TryAddItem(reward.ItemId, reward.Count);
                    if (!lastResult.Success || lastResult.Remainder > 0)
                        return InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.NoSpace, reward.ItemId, reward.Count);
                }
            }

            transaction.Commit();
            return lastResult.Success
                ? lastResult
                : InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.None);
        }

        private static bool CanCraftWithSimulatedTransaction(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason)
        {
            using InventoryGroupTransaction transaction = InventoryGroupTransaction.Begin(group);

            for (int i = 0; i < costs.Count; i++)
            {
                InventoryRecipeEntry cost = costs[i];
                InventoryChangeResult removeResult = group.TryRemoveItem(cost.ItemId, cost.Count);
                if (!removeResult.Success || removeResult.Remainder > 0)
                    return Fail(out reason, InventoryFailReason.NoChange);
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                InventoryRecipeEntry reward = rewards[i];
                if (!group.ItemDatabase.TryGetDefinition(reward.ItemId, out ItemDefinition definition))
                    return Fail(out reason, InventoryFailReason.DefinitionNotFound);

                if (!CanAddReward(group, reward, definition, out reason))
                    return false;
            }

            reason = InventoryFailReason.None;
            return true;
        }

        private static bool CanAddReward(
            InventoryGroup group,
            InventoryRecipeEntry reward,
            ItemDefinition definition,
            out InventoryFailReason reason)
        {
            IReadOnlyList<InventoryContainer> containers = group.Containers;
            int addableTotal = 0;

            for (int i = 0; i < containers.Count; i++)
            {
                InventoryContainer container = containers[i];
                if (container.CanAddItem(reward.ItemId, reward.Count, out reason, out int addable))
                    addableTotal += addable;
            }

            if (addableTotal >= reward.Count)
            {
                reason = InventoryFailReason.None;
                return true;
            }

            reason = InventoryFailReason.NoSpace;
            return false;
        }

        private static bool Fail(out InventoryFailReason reason, InventoryFailReason value)
        {
            reason = value;
            return false;
        }
    }
}
