using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class DefaultItemContainerRouter : IItemContainerRouter
    {
        private static readonly ContainerKind[] MainOnlyRoute =
            { (ContainerKind)InventoryEnumCore.MainContainerKindValue };

        private readonly Dictionary<ItemType, ContainerKind[]> routeTable;

        public DefaultItemContainerRouter(Dictionary<ItemType, ContainerKind[]> routeTable = null)
        {
            this.routeTable = routeTable ?? InventoryEnumRoutes.CreateItemTypeRouteTable();
        }

        public bool TryResolveContainer(InventoryGroup group, in ItemDefinition definition, out IInventoryContainer container)
        {
            container = null;
            if (group == null)
                return false;

            if (!routeTable.TryGetValue(definition.ItemType, out ContainerKind[] kinds))
                kinds = MainOnlyRoute;

            for (int i = 0; i < kinds.Length; i++)
            {
                if (group.TryGetContainerByKind(kinds[i], out container))
                    return true;
            }

            return group.TryGetContainerByKind(
                (ContainerKind)InventoryEnumCore.MainContainerKindValue,
                out container);
        }

        public static DefaultItemContainerRouter CreateDefault() => new();
    }
}
