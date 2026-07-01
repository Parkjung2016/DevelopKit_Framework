using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    internal static class InventoryTestFixtures
    {
        public static void UseSharedCatalog() => ItemCatalog.Set(InventoryTestItemDatabase.Shared);

        public static InventoryContainer CreateMainContainer(int slotCount = 10, string containerId = "main") =>
            new(slotCount, InventoryTestItemDatabase.Shared, InventoryContainerDescriptor.Main(containerId));

        public static InventoryContainer CreateEquipmentContainer(int slotCount = 5, string containerId = "equipment") =>
            new(
                slotCount,
                InventoryTestItemDatabase.Shared,
                new InventoryContainerDescriptor(
                    containerId,
                    (ContainerKind)InventoryTestValues.EquipmentKind,
                    new ItemTypeSlotRule((ItemType)InventoryTestValues.EquipmentType)));

        public static InventoryContainer CreateQuestContainer(int slotCount = 5, string containerId = "quest") =>
            new(
                slotCount,
                InventoryTestItemDatabase.Shared,
                new InventoryContainerDescriptor(containerId, (ContainerKind)InventoryTestValues.QuestKind));

        public static InventoryContainer CreateContainerWithoutDatabase(int slotCount = 5) =>
            new(slotCount, null, InventoryContainerDescriptor.Main());

        public static InventoryGroup CreateGroup(params InventoryContainer[] containers)
        {
            var group = new InventoryGroup(InventoryTestItemDatabase.Shared);
            foreach (InventoryContainer container in containers)
                group.RegisterContainer(container);

            return group;
        }

        public static void FillContainer(InventoryContainer container, int itemId, int count)
        {
            InventoryChangeResult result = container.TryAddItem(itemId, count);
            if (!result.Success || result.Remainder > 0)
                throw new InvalidOperationException($"Failed to fill container with item {itemId} x{count}.");
        }
    }
}
