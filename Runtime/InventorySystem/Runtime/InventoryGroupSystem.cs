using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public class InventoryGroupSystem : MonoBehaviour
    {
        public event Action<InventoryChangeResult> OnInventoryChanged;

        [field: SerializeField] public InventorySystem[] inventorySystems { get; private set; }

        private InventoryGroup group;
        private IInventoryOwner owner;

        public InventoryGroup Group => group;

        public void Init(IInventoryOwner owner, InventorySetupSO setup, IItemContainerRouter router = null)
        {
            if (setup == null)
            {
                CDebug.LogWarning("InventoryGroupSystem : InventorySetupSO is null.");
                return;
            }

            Init(owner, setup.CreateDataProvider(), router);
        }

        public void Init(IInventoryOwner owner, IInventoryDataProvider dataProvider, IItemContainerRouter router = null)
        {
            Init(owner, dataProvider?.ItemDatabase, router);
            group?.SetDataServices(dataProvider?.RecipeDatabase, dataProvider?.LootTableDatabase);
        }

        public void Init(IInventoryOwner owner, IItemDatabase itemDatabase, IItemContainerRouter router = null)
        {
            this.owner = owner;
            group?.Dispose();
            group = new InventoryGroup(itemDatabase, router);

            if (inventorySystems == null)
                return;

            foreach (InventorySystem inventorySystem in inventorySystems)
            {
                if (inventorySystem == null)
                    continue;

                inventorySystem.Init(owner, itemDatabase);
                group.RegisterContainer(inventorySystem.Container);
                inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }
        }

        private void OnDestroy()
        {
            if (inventorySystems == null)
                return;

            foreach (InventorySystem inventorySystem in inventorySystems)
            {
                if (inventorySystem != null)
                    inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
            }

            group?.Dispose();
        }

        public InventoryChangeResult TryAddItem(int itemId, int count)
        {
            InventoryChangeResult result = group.TryAddItem(itemId, count);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public InventoryChangeResult TryRemoveItem(int itemId, int count)
        {
            InventoryChangeResult result = group.TryRemoveItem(itemId, count);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public InventoryChangeResult TryMoveBetween(
            string fromContainerId,
            int fromSlotIndex,
            string toContainerId,
            int toSlotIndex)
        {
            InventoryChangeResult result = group.TryMoveBetween(fromContainerId, fromSlotIndex, toContainerId, toSlotIndex);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public bool HasItem(int itemId, int count = 1) => group != null && group.HasItem(itemId, count);

        public int GetItemCount(int itemId) => group?.GetItemCount(itemId) ?? 0;

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

        public InventoryChangeResult TryCraft(
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards)
        {
            if (group == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.DatabaseNotReady);

            InventoryChangeResult result = group.TryCraft(costs, rewards);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public InventoryChangeResult TryCraft(RecipeSO recipe)
        {
            if (group == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.DatabaseNotReady);

            InventoryChangeResult result = group.TryCraft(recipe);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public bool CanCraft(string recipeId, out InventoryFailReason reason) =>
            group != null
                ? group.CanCraft(recipeId, out reason)
                : FailCraftQuery(out reason);

        public InventoryChangeResult TryCraft(string recipeId)
        {
            if (group == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.DatabaseNotReady);

            InventoryChangeResult result = group.TryCraft(recipeId);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public InventoryChangeResult TryGrantLoot(string tableId, System.Random random = null)
        {
            if (group == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady);

            InventoryChangeResult result = group.TryGrantLoot(tableId, random);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public InventoryChangeResult TryGrantLoot(LootTableSO table, System.Random random = null)
        {
            if (group == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady);

            InventoryChangeResult result = group.TryGrantLoot(table, random);
            if (result.Success)
                OnInventoryChanged?.Invoke(result);

            return result;
        }

        public InventoryGroupDelta ComputeDelta(InventoryGroupSaveData baseline) =>
            group?.ComputeDelta(baseline) ?? new InventoryGroupDelta();

        public InventoryGroupTransaction BeginTransaction() =>
            group?.BeginTransaction() ?? throw new InvalidOperationException("InventoryGroupSystem : not initialized.");

        public InventoryGroupSaveData ExportSaveData() =>
            group == null ? new InventoryGroupSaveData() : InventorySerializer.Export(group);

        public InventoryImportReport ImportSaveDataWithReport(InventoryGroupSaveData saveData)
        {
            if (group == null)
            {
                CDebug.LogWarning("InventoryGroupSystem : not initialized.");
                return new InventoryImportReport
                {
                    LastResult = InventoryChangeResult.Fail(InventoryChangeType.Clear, InventoryFailReason.DatabaseNotReady)
                };
            }

            return InventorySerializer.ImportWithReport(group, saveData);
        }

        public void ImportSaveData(InventoryGroupSaveData saveData) =>
            ImportSaveDataWithReport(saveData);

        private void HandleInventoryChanged(InventoryChangeResult result) => OnInventoryChanged?.Invoke(result);

        private static bool FailCraftQuery(out InventoryFailReason reason)
        {
            reason = InventoryFailReason.DatabaseNotReady;
            return false;
        }
    }
}
