using System.Collections.Generic;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Tests
{
    internal static class EquipmentTestValues
    {
        public const int GeneralItemId = 1001;
        public const int WeaponItemId = 3001;
        public const int HeadItemId = 3002;
        public const int ChestItemId = 3003;

        public const int EquipmentKind = 1;
        public const int EquipmentType = 3;
    }

    internal sealed class EquipmentTestItemDatabase : IItemDatabase
    {
        public static readonly EquipmentTestItemDatabase Shared = new();

        private readonly Dictionary<int, ItemDefinition> definitions = new()
        {
            [EquipmentTestValues.GeneralItemId] = new(EquipmentTestValues.GeneralItemId),
            [EquipmentTestValues.WeaponItemId] = new(
                EquipmentTestValues.WeaponItemId,
                maxStackSize: 1,
                isStackable: false,
                itemType: (ItemType)EquipmentTestValues.EquipmentType),
            [EquipmentTestValues.HeadItemId] = new(
                EquipmentTestValues.HeadItemId,
                maxStackSize: 1,
                isStackable: false,
                itemType: (ItemType)EquipmentTestValues.EquipmentType),
            [EquipmentTestValues.ChestItemId] = new(
                EquipmentTestValues.ChestItemId,
                maxStackSize: 1,
                isStackable: false,
                itemType: (ItemType)EquipmentTestValues.EquipmentType)
        };

        public bool TryGetDefinition(int itemId, out ItemDefinition definition) =>
            definitions.TryGetValue(itemId, out definition);
    }

    internal static class EquipmentTestFixtures
    {
        public static IEquipmentItemProfileSource CreateProfileSource() =>
            new DictionaryEquipmentProfileSource(new Dictionary<int, string>
            {
                [EquipmentTestValues.WeaponItemId] = EquipmentSlotCategories.Weapon,
                [EquipmentTestValues.HeadItemId] = EquipmentSlotCategories.Head,
                [EquipmentTestValues.ChestItemId] = EquipmentSlotCategories.Chest
            });

        public static InventoryContainer CreateCategorizedEquipmentContainer(int slotCount = 6, string containerId = "equipment")
        {
            IEquipmentItemProfileSource profileSource = CreateProfileSource();
            var slotRule = new EquipmentSlotRule(
                new[]
                {
                    EquipmentSlotCategories.Weapon,
                    EquipmentSlotCategories.Head,
                    EquipmentSlotCategories.Chest,
                    EquipmentSlotCategories.Hands,
                    EquipmentSlotCategories.Feet,
                    EquipmentSlotCategories.Any
                },
                profileSource,
                (ItemType)EquipmentTestValues.EquipmentType);

            return new InventoryContainer(
                slotCount,
                EquipmentTestItemDatabase.Shared,
                new InventoryContainerDescriptor(
                    containerId,
                    (ContainerKind)EquipmentTestValues.EquipmentKind,
                    slotRule));
        }

        public static InventoryGroup CreateGroup()
        {
            var group = new InventoryGroup(EquipmentTestItemDatabase.Shared);
            group.RegisterContainer(new InventoryContainer(
                10,
                EquipmentTestItemDatabase.Shared,
                InventoryContainerDescriptor.Main("main")));
            group.RegisterContainer(CreateCategorizedEquipmentContainer());
            return group;
        }
    }
}
