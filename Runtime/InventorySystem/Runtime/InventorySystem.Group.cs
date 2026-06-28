using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public partial class InventorySystem
    {
        public bool CanCraft(
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason) =>
            group != null
                ? group.CanCraft(costs, rewards, out reason)
                : FailCraftQuery(out reason);

        public bool CanCraft(RecipeSO recipe, out InventoryFailReason reason) =>
            group != null
                ? group.CanCraft(recipe, out reason)
                : FailCraftQuery(out reason);

        public bool CanCraft(string recipeId, out InventoryFailReason reason) =>
            group != null
                ? group.CanCraft(recipeId, out reason)
                : FailCraftQuery(out reason);

        public InventoryChangeResult TryCraft(
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards) =>
            ExecuteGroup(
                () => group == null
                    ? InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.DatabaseNotReady)
                    : group.TryCraft(costs, rewards),
                0,
                0,
                InventoryChangeType.Craft);

        public InventoryChangeResult TryCraft(RecipeSO recipe) =>
            ExecuteGroup(
                () => group == null
                    ? InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.DatabaseNotReady)
                    : group.TryCraft(recipe),
                0,
                0,
                InventoryChangeType.Craft);

        public InventoryChangeResult TryCraft(string recipeId) =>
            ExecuteGroup(
                () => group == null
                    ? InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.DatabaseNotReady)
                    : group.TryCraft(recipeId),
                0,
                0,
                InventoryChangeType.Craft);

        public InventoryChangeResult TryGrantLoot(string tableId, System.Random random = null) =>
            ExecuteGroup(
                () => group == null
                    ? InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady)
                    : group.TryGrantLoot(tableId, random),
                0,
                0,
                InventoryChangeType.Add);

        public InventoryChangeResult TryGrantLoot(LootTableSO table, System.Random random = null) =>
            ExecuteGroup(
                () => group == null
                    ? InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady)
                    : group.TryGrantLoot(table, random),
                0,
                0,
                InventoryChangeType.Add);

        public InventoryGroupDelta ComputeDelta(InventoryGroupSaveData baseline) =>
            group?.ComputeDelta(baseline) ?? new InventoryGroupDelta();

        public InventoryGroupTransaction BeginTransaction() =>
            group?.BeginTransaction()
            ?? throw new InvalidOperationException("InventorySystem : not initialized.");

        public InventoryGroupSaveData ExportGroupSaveData() =>
            group == null ? new InventoryGroupSaveData() : InventorySerializer.Export(group);

        public InventoryImportReport ImportGroupSaveDataWithReport(InventoryGroupSaveData saveData)
        {
            if (group == null)
            {
                return new InventoryImportReport
                {
                    LastResult = InventoryChangeResult.Fail(InventoryChangeType.Clear, InventoryFailReason.DatabaseNotReady)
                };
            }

            return InventorySerializer.ImportWithReport(group, saveData);
        }

        public void ImportGroupSaveData(InventoryGroupSaveData saveData) =>
            ImportGroupSaveDataWithReport(saveData);

        private static bool FailCraftQuery(out InventoryFailReason reason)
        {
            reason = InventoryFailReason.DatabaseNotReady;
            return false;
        }
    }
}
