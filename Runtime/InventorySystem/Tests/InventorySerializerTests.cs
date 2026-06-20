using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventorySerializerTests
    {
        private InventoryContainer main;
        private InventoryContainer equipment;
        private InventoryGroup group;

        [SetUp]
        public void SetUp()
        {
            main = InventoryTestFixtures.CreateMainContainer();
            equipment = InventoryTestFixtures.CreateEquipmentContainer();
            group = InventoryTestFixtures.CreateGroup(main, equipment);
        }

        [TearDown]
        public void TearDown()
        {
            group?.Dispose();
            group = null;
            main = null;
            equipment = null;
        }

        [Test]
        public void Export_And_Import_RoundTripsContainerState()
        {
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 4);
            main.TryAddItemToSlot(2, InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryContainerSaveData saveData = InventorySerializer.Export(main);

            main.ClearAll();
            InventoryChangeResult importResult = InventorySerializer.Import(main, saveData);

            Assert.IsTrue(importResult.Success);
            Assert.AreEqual(4, main.GetSlot(0).Stack.Count);
            Assert.AreEqual(InventoryTestItemDatabase.EquipmentItemId, main.GetSlot(2).Stack.ItemId);
        }

        [Test]
        public void ExportGroup_And_Import_RoundTripsAllContainers()
        {
            main.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 2);
            equipment.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryGroupSaveData saveData = InventorySerializer.Export(group);

            main.ClearAll();
            equipment.ClearAll();
            InventorySerializer.Import(group, saveData);

            Assert.AreEqual(2, main.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
            Assert.AreEqual(1, equipment.GetItemCount(InventoryTestItemDatabase.EquipmentItemId));
        }
    }
}
