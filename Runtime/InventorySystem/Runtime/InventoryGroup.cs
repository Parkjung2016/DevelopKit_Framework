using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class InventoryGroup : IDisposable
    {
        private readonly Dictionary<string, InventoryContainer> containersById = new();
        private readonly Dictionary<ContainerKind, InventoryContainer> containersByKind = new();
        private readonly List<InventoryContainer> containers = new();
        private readonly IItemDatabase itemDatabase;
        private IItemContainerRouter router;
        private IRecipeDatabase recipeDatabase = NullRecipeDatabase.Instance;
        private ILootTableDatabase lootTableDatabase = NullLootTableDatabase.Instance;

        public IReadOnlyList<InventoryContainer> Containers => containers;
        public IItemDatabase ItemDatabase => itemDatabase;
        public IRecipeDatabase RecipeDatabase => recipeDatabase;
        public ILootTableDatabase LootTableDatabase => lootTableDatabase;

        public InventoryGroup(IItemDatabase itemDatabase, IItemContainerRouter router = null)
        {
            this.itemDatabase = itemDatabase;
            this.router = router ?? DefaultItemContainerRouter.CreateDefault();
        }

        public InventoryGroup(IInventoryDataProvider dataProvider, IItemContainerRouter router = null)
            : this(dataProvider?.ItemDatabase, router)
        {
            if (dataProvider != null)
                SetDataServices(dataProvider.RecipeDatabase, dataProvider.LootTableDatabase);
        }

        public void SetDataServices(
            IRecipeDatabase recipes = null,
            ILootTableDatabase lootTables = null)
        {
            recipeDatabase = recipes ?? NullRecipeDatabase.Instance;
            lootTableDatabase = lootTables ?? NullLootTableDatabase.Instance;
        }

        public void SetRouter(IItemContainerRouter router) =>
            this.router = router ?? DefaultItemContainerRouter.CreateDefault();

        public void RegisterContainer(InventoryContainer container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            if (containersById.ContainsKey(container.ContainerId))
            {
                CDebug.LogWarning($"InventoryGroup : container id {container.ContainerId} already registered.");
                return;
            }

            containersById.Add(container.ContainerId, container);
            containers.Add(container);

            if (!containersByKind.ContainsKey(container.Kind))
                containersByKind.Add(container.Kind, container);
        }

        public void UnregisterContainer(string containerId)
        {
            if (!containersById.Remove(containerId, out InventoryContainer container))
                return;

            containers.Remove(container);

            if (containersByKind.TryGetValue(container.Kind, out InventoryContainer mapped) && mapped == container)
                containersByKind.Remove(container.Kind);
        }

        public bool TryGetContainer(string containerId, out InventoryContainer container) =>
            containersById.TryGetValue(containerId, out container);

        public bool TryGetContainerByKind(ContainerKind kind, out IInventoryContainer container)
        {
            if (containersByKind.TryGetValue(kind, out InventoryContainer found))
            {
                container = found;
                return true;
            }

            container = null;
            return false;
        }

        public InventoryChangeResult TryAddItem(int itemId, int count)
        {
            if (itemDatabase == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (!itemDatabase.TryGetDefinition(itemId, out ItemDefinition definition))
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DefinitionNotFound, itemId, count);

            if (!router.TryResolveContainer(this, definition, out IInventoryContainer container))
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.ContainerNotFound, itemId, count);

            InventoryChangeResult result = container.TryAddItem(itemId, count);
            if (result.Remainder <= 0 || container.Kind == (ContainerKind)InventoryEnumCore.MainContainerKindValue)
                return result;

            if (!TryGetContainerByKind((ContainerKind)InventoryEnumCore.MainContainerKindValue, out IInventoryContainer fallback))
                return result;

            InventoryChangeResult fallbackResult = fallback.TryAddItem(itemId, result.Remainder);
            return MergeAddResults(result, fallbackResult);
        }

        public InventoryChangeResult TryAddItemToContainer(string containerId, int itemId, int count)
        {
            if (!TryGetContainer(containerId, out InventoryContainer container))
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.ContainerNotFound, itemId, count);

            return container.TryAddItem(itemId, count);
        }

        public InventoryChangeResult TryRemoveItem(int itemId, int count)
        {
            int remainder = count;
            InventoryChangeResult lastResult = InventoryChangeResult.Fail(InventoryChangeType.Remove, InventoryFailReason.NoChange, itemId, count);

            foreach (InventoryContainer container in containers)
            {
                if (remainder <= 0)
                    break;

                lastResult = container.TryRemoveItem(itemId, remainder);
                if (!lastResult.Success)
                    continue;

                remainder = lastResult.Remainder;
            }

            return lastResult;
        }

        public InventoryChangeResult TryMoveBetween(
            string fromContainerId,
            int fromSlotIndex,
            string toContainerId,
            int toSlotIndex)
        {
            if (!TryGetContainer(fromContainerId, out InventoryContainer fromContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, primarySlotIndex: fromSlotIndex);

            if (!TryGetContainer(toContainerId, out InventoryContainer toContainer))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.ContainerNotFound, secondarySlotIndex: toSlotIndex);

            if (fromContainer == toContainer)
                return fromContainer.TryMoveSlot(fromSlotIndex, toSlotIndex);

            if (!fromContainer.TryGetSlot(fromSlotIndex, out InventorySlot fromSlot) || fromSlot.IsEmpty)
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.NoChange, primarySlotIndex: fromSlotIndex);

            int itemId = fromSlot.Stack.ItemId;
            int count = fromSlot.Stack.Count;

            if (!toContainer.TryGetSlot(toSlotIndex, out InventorySlot toSlot))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.InvalidSlotIndex, itemId, secondarySlotIndex: toSlotIndex);

            if (!toSlot.IsEmpty && toSlot.Stack.ItemId != itemId)
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.SlotMismatch, itemId, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex);

            if (!itemDatabase.TryGetDefinition(itemId, out ItemDefinition definition))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.DefinitionNotFound, itemId);

            if (!toContainer.CanAcceptSlot(toSlotIndex, definition))
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.SlotRuleDenied, itemId, primarySlotIndex: fromSlotIndex, secondarySlotIndex: toSlotIndex);

            InventoryChangeResult removeResult = fromContainer.TryRemoveItemFromSlot(fromSlotIndex, count);
            if (!removeResult.Success)
                return removeResult;

            InventoryChangeResult addResult = toContainer.TryAddItemToSlot(toSlotIndex, itemId, count);
            if (addResult.Success)
                return addResult.WithSecondaryContainer(toContainerId);

            fromContainer.TryAddItemToSlot(fromSlotIndex, itemId, removeResult.ProcessedCount);
            return addResult;
        }

        public bool HasItem(int itemId, int count = 1) => GetItemCount(itemId) >= count;

        public int GetItemCount(int itemId)
        {
            int total = 0;
            foreach (InventoryContainer container in containers)
                total += container.GetItemCount(itemId);

            return total;
        }

        public int GetItemCount(string containerId, int itemId) =>
            TryGetContainer(containerId, out InventoryContainer container) ? container.GetItemCount(itemId) : 0;

        public bool CanCraft(
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards,
            out InventoryFailReason reason) =>
            InventoryCrafting.CanCraft(this, costs, rewards, out reason);

        public InventoryChangeResult TryCraft(
            IReadOnlyList<InventoryRecipeEntry> costs,
            IReadOnlyList<InventoryRecipeEntry> rewards) =>
            InventoryCrafting.TryCraft(this, costs, rewards);

        public bool CanCraft(RecipeSO recipe, out InventoryFailReason reason) =>
            InventoryCrafting.CanCraft(this, recipe, out reason);

        public InventoryChangeResult TryCraft(RecipeSO recipe) =>
            InventoryCrafting.TryCraft(this, recipe);

        public bool CanCraft(string recipeId, out InventoryFailReason reason)
        {
            if (!recipeDatabase.TryGetRecipe(recipeId, out RecipeDefinition recipe))
                return FailCraftLookup(out reason, InventoryFailReason.RecipeNotFound);

            return CanCraft(recipe.Costs, recipe.Rewards, out reason);
        }

        public bool CanCraft(in RecipeDefinition recipe, out InventoryFailReason reason) =>
            InventoryCrafting.CanCraft(this, recipe, out reason);

        public InventoryChangeResult TryCraft(string recipeId)
        {
            if (!recipeDatabase.TryGetRecipe(recipeId, out RecipeDefinition recipe))
                return InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.RecipeNotFound);

            return TryCraft(recipe);
        }

        public InventoryChangeResult TryCraft(in RecipeDefinition recipe) =>
            InventoryCrafting.TryCraft(this, recipe);

        public InventoryChangeResult TryGrantLoot(string tableId, System.Random random = null)
        {
            if (!lootTableDatabase.TryGetTable(tableId, out LootTableDefinition table))
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.LootTableNotFound);

            return LootRoller.TryGrantLoot(this, table, random);
        }

        public InventoryChangeResult TryGrantLoot(in LootTableDefinition table, System.Random random = null) =>
            LootRoller.TryGrantLoot(this, table, random);

        public InventoryChangeResult TryGrantLoot(LootTableSO table, System.Random random = null) =>
            LootRoller.TryGrantLoot(this, table, random);

        public InventoryGroupSaveData ExportSaveData() => InventorySerializer.Export(this);

        public InventoryGroupDelta ComputeDelta(InventoryGroupSaveData baseline) =>
            InventoryDeltaComputer.ComputeGroupDelta(this, baseline);

        public InventoryGroupTransaction BeginTransaction() => InventoryGroupTransaction.Begin(this);

        public void Dispose()
        {
            foreach (InventoryContainer container in containers)
                container.Dispose();

            containers.Clear();
            containersById.Clear();
            containersByKind.Clear();
        }

        private static InventoryChangeResult MergeAddResults(InventoryChangeResult primary, InventoryChangeResult secondary)
        {
            if (!secondary.Success)
                return primary;

            return InventoryChangeResult.Succeed(
                InventoryChangeType.Add,
                secondary.ItemId,
                secondary.Definition,
                secondary.SecondaryDefinition,
                primary.RequestedCount,
                primary.ProcessedCount + secondary.ProcessedCount,
                secondary.Remainder,
                primary.TotalItemCountBefore,
                secondary.TotalItemCountAfter,
                secondary.PrimarySlotIndex,
                secondary.SecondarySlotIndex,
                primary.ItemWasAcquired || secondary.ItemWasAcquired,
                false,
                primary.ContainerId,
                primary.Kind,
                secondary.ContainerId,
                MergeIndices(primary.ChangedSlotIndices, secondary.ChangedSlotIndices),
                MergeSlotChanges(primary.SlotChanges, secondary.SlotChanges));
        }

        private static int[] MergeIndices(int[] a, int[] b)
        {
            if (a == null || a.Length == 0)
                return b ?? Array.Empty<int>();

            if (b == null || b.Length == 0)
                return a;

            var merged = new int[a.Length + b.Length];
            Array.Copy(a, merged, a.Length);
            Array.Copy(b, 0, merged, a.Length, b.Length);
            return merged;
        }

        private static InventorySlotChange[] MergeSlotChanges(InventorySlotChange[] a, InventorySlotChange[] b)
        {
            if (a == null || a.Length == 0)
                return b ?? Array.Empty<InventorySlotChange>();

            if (b == null || b.Length == 0)
                return a;

            var merged = new InventorySlotChange[a.Length + b.Length];
            Array.Copy(a, merged, a.Length);
            Array.Copy(b, 0, merged, a.Length, b.Length);
            return merged;
        }

        private static bool FailCraftLookup(out InventoryFailReason reason, InventoryFailReason value)
        {
            reason = value;
            return false;
        }
    }
}
