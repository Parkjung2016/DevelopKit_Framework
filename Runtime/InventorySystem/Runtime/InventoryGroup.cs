using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryGroup : IDisposable
    {
        private readonly Dictionary<string, InventoryContainer> containersById = new();
        private readonly Dictionary<ContainerKind, InventoryContainer> containersByKind = new();
        private readonly List<InventoryContainer> containers = new();
        private readonly IItemDatabase itemDatabaseOverride;
        private IItemContainerRouter router;
        private IRecipeDatabase recipeDatabaseOverride;
        private ILootTableDatabase lootTableDatabaseOverride;
        private IItemInstanceFactory itemInstanceFactory;
        private IItemInstanceIdGenerator instanceIdGenerator;

        public IItemInstanceStore ItemInstanceStore { get; } = new InMemoryItemInstanceStore();

        public IItemInstanceFactory ItemInstanceFactory
        {
            get => itemInstanceFactory;
            set
            {
                if (ReferenceEquals(itemInstanceFactory, value))
                    return;

                itemInstanceFactory = value;
                RebindInstanceServices();
            }
        }

        public IItemInstanceIdGenerator InstanceIdGenerator
        {
            get => instanceIdGenerator;
            set
            {
                if (ReferenceEquals(instanceIdGenerator, value))
                    return;

                instanceIdGenerator = value;
                RebindInstanceServices();
            }
        }

        public IReadOnlyList<InventoryContainer> Containers => containers;
        public IItemDatabase ItemDatabase => ItemCatalog.Resolve(itemDatabaseOverride);
        public IRecipeDatabase RecipeDatabase => RecipeCatalog.Resolve(recipeDatabaseOverride);
        public ILootTableDatabase LootTableDatabase => LootTableCatalog.Resolve(lootTableDatabaseOverride);

        public InventoryGroup(IItemDatabase itemDatabase = null, IItemContainerRouter router = null)
        {
            itemDatabaseOverride = itemDatabase;
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
            recipeDatabaseOverride = recipes;
            lootTableDatabaseOverride = lootTables;
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
            container.BindInstanceServices(ItemInstanceStore, ItemInstanceFactory, InstanceIdGenerator);

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

        private void RebindInstanceServices()
        {
            for (int i = 0; i < containers.Count; i++)
            {
                containers[i].BindInstanceServices(
                    ItemInstanceStore,
                    itemInstanceFactory,
                    instanceIdGenerator);
            }
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
            if (!ItemCatalog.IsReady && itemDatabaseOverride == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady, itemId, count);

            if (!ItemDatabase.TryGetDefinition(itemId, out ItemDefinition definition))
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
            if (count <= 0)
                return InventoryChangeResult.Fail(
                    InventoryChangeType.Remove,
                    InventoryFailReason.InvalidCount,
                    itemId,
                    count);

            int totalBefore = GetItemCount(itemId);
            int remainder = count;
            bool hasSuccess = false;
            InventoryChangeResult combined = default;
            InventoryChangeResult lastFailure = InventoryChangeResult.Fail(
                InventoryChangeType.Remove,
                InventoryFailReason.ItemNotFound,
                itemId,
                count,
                totalItemCountBefore: totalBefore);

            for (int i = 0; i < containers.Count && remainder > 0; i++)
            {
                InventoryChangeResult current = containers[i].TryRemoveItem(itemId, remainder);
                if (!current.Success)
                {
                    lastFailure = current;
                    continue;
                }

                combined = hasSuccess
                    ? MergeRemoveResults(combined, current, count, totalBefore)
                    : current;
                hasSuccess = true;
                remainder -= current.ProcessedCount;
            }

            if (!hasSuccess)
                return lastFailure;

            return NormalizeRemoveResult(combined, count, remainder, totalBefore);
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

        public void Dispose()
        {
            foreach (InventoryContainer container in containers)
                container.Dispose();

            containers.Clear();
            containersById.Clear();
            containersByKind.Clear();
        }
    }
}
