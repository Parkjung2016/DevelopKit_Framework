#if !PJDEV_INVENTORY_ENUMS_GENERATED

using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public enum ItemType
    {
        /// <summary>Unspecified / excluded from filters</summary>
        None = 0,
        /// <summary>General item</summary>
        General = 1,
        /// <summary>Consumable item</summary>
        Consumable = 2,
        /// <summary>Equipment</summary>
        Equipment = 3,
        /// <summary>Material</summary>
        Material = 4,
        /// <summary>Quest item</summary>
        Quest = 5,
        /// <summary>Currency</summary>
        Currency = 6
    }

    public enum ContainerKind
    {
        /// <summary>Default inventory</summary>
        Main = 0,
        /// <summary>Equipment slot</summary>
        Equipment = 1,
        /// <summary>Quick bar</summary>
        QuickBar = 2,
        /// <summary>Stash</summary>
        Stash = 3,
        /// <summary>Quest-only container</summary>
        Quest = 4
    }

    public static class InventoryEnumCatalog
    {
        private static readonly ItemType[] ItemTypeOrder =
        {
            ItemType.None,
            ItemType.General,
            ItemType.Consumable,
            ItemType.Equipment,
            ItemType.Material,
            ItemType.Quest,
            ItemType.Currency,
        };

        private static readonly ContainerKind[] ContainerKindOrder =
        {
            ContainerKind.Main,
            ContainerKind.Equipment,
            ContainerKind.QuickBar,
            ContainerKind.Stash,
            ContainerKind.Quest,
        };

        private static readonly Dictionary<ItemType, string> ItemTypeDisplayNames = new()
        {
            { ItemType.None, "None" },
            { ItemType.General, "General" },
            { ItemType.Consumable, "Consumable" },
            { ItemType.Equipment, "Equipment" },
            { ItemType.Material, "Material" },
            { ItemType.Quest, "Quest" },
            { ItemType.Currency, "Currency" },
        };

        private static readonly Dictionary<ContainerKind, string> ContainerKindDisplayNames = new()
        {
            { ContainerKind.Main, "Main" },
            { ContainerKind.Equipment, "Equipment" },
            { ContainerKind.QuickBar, "QuickBar" },
            { ContainerKind.Stash, "Stash" },
            { ContainerKind.Quest, "Quest" },
        };

        private static readonly Dictionary<ItemType, string> ItemTypeDescriptions = new()
        {
            { ItemType.None, "Unspecified / excluded from filters" },
            { ItemType.General, "General item" },
            { ItemType.Consumable, "Consumable item" },
            { ItemType.Equipment, "Equipment" },
            { ItemType.Material, "Material" },
            { ItemType.Quest, "Quest item" },
            { ItemType.Currency, "Currency" },
        };

        private static readonly Dictionary<ContainerKind, string> ContainerKindDescriptions = new()
        {
            { ContainerKind.Main, "Default inventory" },
            { ContainerKind.Equipment, "Equipment slot" },
            { ContainerKind.QuickBar, "Quick bar" },
            { ContainerKind.Stash, "Stash" },
            { ContainerKind.Quest, "Quest-only container" },
        };

        public static IReadOnlyList<ItemType> ItemTypesInOrder => ItemTypeOrder;

        public static IReadOnlyList<ContainerKind> ContainerKindsInOrder => ContainerKindOrder;

        public static bool TryGetItemTypeDisplayName(ItemType value, out string displayName) =>
            ItemTypeDisplayNames.TryGetValue(value, out displayName);

        public static bool TryGetContainerKindDisplayName(ContainerKind value, out string displayName) =>
            ContainerKindDisplayNames.TryGetValue(value, out displayName);

        public static string GetItemTypeDisplayName(ItemType value) =>
            TryGetItemTypeDisplayName(value, out string displayName) ? displayName : value.ToString();

        public static string GetContainerKindDisplayName(ContainerKind value) =>
            TryGetContainerKindDisplayName(value, out string displayName) ? displayName : value.ToString();

        public static bool TryGetItemTypeDescription(ItemType value, out string description) =>
            ItemTypeDescriptions.TryGetValue(value, out description);

        public static bool TryGetContainerKindDescription(ContainerKind value, out string description) =>
            ContainerKindDescriptions.TryGetValue(value, out description);

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

    public static class InventoryEnumRoutes
    {
        public static Dictionary<ItemType, ContainerKind[]> CreateItemTypeRouteTable()
        {
            var table = new Dictionary<ItemType, ContainerKind[]>();
            table[(ItemType)3] = new[] { (ContainerKind)1, (ContainerKind)0 };
            table[(ItemType)5] = new[] { (ContainerKind)4, (ContainerKind)0 };
            table[(ItemType)2] = new[] { (ContainerKind)2, (ContainerKind)0 };
            table[(ItemType)4] = new[] { (ContainerKind)0, (ContainerKind)3 };
            table[(ItemType)6] = new[] { (ContainerKind)0 };
            table[(ItemType)1] = new[] { (ContainerKind)0 };
            return table;
        }
    }
}

#endif
