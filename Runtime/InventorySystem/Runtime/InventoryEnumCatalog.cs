using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static partial class InventoryEnumCatalog
    {
        public static string GetItemTypeDisplayName(ItemType value) =>
            TryGetItemTypeDisplayName(value, out string displayName) ? displayName : value.ToString();

        public static string GetContainerKindDisplayName(ContainerKind value) =>
            TryGetContainerKindDisplayName(value, out string displayName) ? displayName : value.ToString();

        public static IEnumerable<ItemType> GetSelectableItemTypes()
        {
            IReadOnlyList<ItemType> order = ItemTypesInOrder;
            for (int i = 0; i < order.Count; i++)
            {
                ItemType itemType = order[i];
                if ((int)itemType != InventoryEnumCore.NoneItemTypeValue)
                    yield return itemType;
            }
        }

        public static IEnumerable<ItemType> GetAllItemTypes() => ItemTypesInOrder;

        public static IEnumerable<ContainerKind> GetAllContainerKinds() => ContainerKindsInOrder;
    }
}
