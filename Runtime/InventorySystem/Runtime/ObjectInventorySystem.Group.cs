using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public partial class ObjectInventorySystem
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
            IReadOnlyList<InventoryRecipeEntry> rewards)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Craft)
                : Complete(currentGroup.TryCraft(costs, rewards));
        }

        public InventoryChangeResult TryCraft(RecipeSO recipe)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Craft)
                : Complete(currentGroup.TryCraft(recipe));
        }

        public InventoryChangeResult TryCraft(string recipeId)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Craft)
                : Complete(currentGroup.TryCraft(recipeId));
        }

        public InventoryChangeResult TryGrantLoot(string tableId, IRandomSource random = null)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Add)
                : Complete(currentGroup.TryGrantLoot(tableId, random));
        }

        public InventoryChangeResult TryGrantLoot(LootTableSO table, IRandomSource random = null)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Add)
                : Complete(currentGroup.TryGrantLoot(table, random));
        }
        public InventoryGroupDelta ComputeDelta(InventoryGroupSaveData baseline) =>
            group?.ComputeDelta(baseline) ?? new InventoryGroupDelta();

        public InventoryGroupTransaction BeginTransaction() =>
            group?.BeginTransaction()
            ?? throw new InvalidOperationException("ObjectInventorySystem : not initialized.");

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
