using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventoryCrafting
    {
        public static bool CanCraft(
            InventoryGroup group,
            in RecipeDefinition recipe,
            out InventoryFailReason reason) =>
            CanCraft(group, recipe.Costs, recipe.Rewards, out reason);

        public static InventoryChangeResult TryCraft(InventoryGroup group, in RecipeDefinition recipe) =>
            TryCraft(group, recipe.Costs, recipe.Rewards);

        public static bool CanCraft(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason)
        {
            if (!ValidateRequest(group, costs, rewards, out reason))
                return false;

            if (rewards == null || rewards.Count == 0)
                return true;

            using InventoryGroupTransaction transaction = InventoryGroupTransaction.Begin(group);
            if (!TryConsumeCosts(group, costs, out reason, out _))
                return false;

            return TryAddRewards(group, rewards, out reason, out _);
        }

        public static InventoryChangeResult TryCraft(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards)
        {
            if (!ValidateRequest(group, costs, rewards, out InventoryFailReason reason))
                return InventoryChangeResult.Fail(InventoryChangeType.Craft, reason);

            using InventoryGroupTransaction transaction = InventoryGroupTransaction.Begin(group);
            if (!TryConsumeCosts(group, costs, out reason, out InventoryChangeResult lastResult))
            {
                return InventoryChangeResult.Fail(
                    InventoryChangeType.Craft,
                    reason,
                    lastResult.ItemId,
                    lastResult.RequestedCount);
            }

            if (rewards != null && rewards.Count > 0 &&
                !TryAddRewards(group, rewards, out reason, out lastResult))
            {
                return InventoryChangeResult.Fail(
                    InventoryChangeType.Craft,
                    reason,
                    lastResult.ItemId,
                    lastResult.RequestedCount);
            }

            transaction.Commit();
            return rewards != null && rewards.Count > 0
                ? lastResult
                : CreateCostOnlySuccess(lastResult);
        }

        private static bool ValidateRequest(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason)
        {
            if (group == null)
                return Fail(out reason, InventoryFailReason.DatabaseNotReady);

            if (costs == null || costs.Count == 0)
                return Fail(out reason, InventoryFailReason.InvalidCount);

            for (int i = 0; i < costs.Count; i++)
            {
                InventoryRecipeEntry cost = costs[i];
                if (cost.ItemId <= 0 || cost.Count <= 0)
                    return Fail(out reason, InventoryFailReason.InvalidCount);

                if (!group.HasItem(cost.ItemId, cost.Count))
                    return Fail(out reason, InventoryFailReason.NoChange);
            }

            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    InventoryRecipeEntry reward = rewards[i];
                    if (reward.ItemId <= 0 || reward.Count <= 0)
                        return Fail(out reason, InventoryFailReason.InvalidCount);

                    if (!group.ItemDatabase.TryGetDefinition(reward.ItemId, out _))
                        return Fail(out reason, InventoryFailReason.DefinitionNotFound);
                }
            }

            reason = InventoryFailReason.None;
            return true;
        }

        private static bool TryConsumeCosts(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> costs,
            out InventoryFailReason reason,
            out InventoryChangeResult lastResult)
        {
            lastResult = default;
            for (int i = 0; i < costs.Count; i++)
            {
                InventoryRecipeEntry cost = costs[i];
                lastResult = group.TryRemoveItem(cost.ItemId, cost.Count);
                if (!lastResult.Success || lastResult.Remainder > 0)
                    return Fail(out reason, InventoryFailReason.NoChange);
            }

            reason = InventoryFailReason.None;
            return true;
        }

        private static bool TryAddRewards(
            InventoryGroup group,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason,
            out InventoryChangeResult lastResult)
        {
            lastResult = default;
            for (int i = 0; i < rewards.Count; i++)
            {
                InventoryRecipeEntry reward = rewards[i];
                lastResult = group.TryAddItem(reward.ItemId, reward.Count);
                if (!lastResult.Success || lastResult.Remainder > 0)
                    return Fail(out reason, InventoryFailReason.NoSpace);
            }

            reason = InventoryFailReason.None;
            return true;
        }

        private static InventoryChangeResult CreateCostOnlySuccess(InventoryChangeResult lastResult) =>
            InventoryChangeResult.Succeed(
                InventoryChangeType.Craft,
                lastResult.ItemId,
                lastResult.Definition,
                default,
                1,
                1,
                0,
                lastResult.TotalItemCountBefore,
                lastResult.TotalItemCountAfter,
                lastResult.PrimarySlotIndex,
                lastResult.SecondarySlotIndex,
                false,
                lastResult.ItemWasDepleted,
                lastResult.ContainerId,
                lastResult.Kind,
                lastResult.SecondaryContainerId,
                lastResult.ChangedSlotIndices,
                lastResult.SlotChanges);

        private static bool Fail(out InventoryFailReason reason, InventoryFailReason value)
        {
            reason = value;
            return false;
        }
    }
}