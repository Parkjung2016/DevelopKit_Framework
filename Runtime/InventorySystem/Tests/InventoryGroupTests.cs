using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryGroupTests
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
        public void TryAddItem_RoutesEquipmentToEquipmentContainer()
        {
            InventoryChangeResult result = group.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("equipment", result.ContainerId);
            Assert.AreEqual((ContainerKind)InventoryTestValues.EquipmentKind, result.Kind);
            Assert.AreEqual(1, equipment.GetItemCount(InventoryTestItemDatabase.EquipmentItemId));
            Assert.AreEqual(0, main.GetItemCount(InventoryTestItemDatabase.EquipmentItemId));
        }

        [Test]
        public void TryAddItem_RoutesGeneralToMainContainer()
        {
            InventoryChangeResult result = group.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 3);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("main", result.ContainerId);
            Assert.AreEqual(3, main.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void TryAddItem_EquipmentFull_FallsBackToMain()
        {
            InventoryTestFixtures.FillContainer(equipment, InventoryTestItemDatabase.EquipmentItemId, 5);

            InventoryChangeResult result = group.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, main.GetItemCount(InventoryTestItemDatabase.EquipmentItemId));
        }

        [Test]
        public void TryRemoveItem_RemovesAcrossContainers()
        {
            group.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 3);
            group.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryChangeResult result = group.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void TryMoveBetween_MovesItemAcrossContainers()
        {
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryChangeResult result = group.TryMoveBetween("main", 0, "equipment", 0);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(main.IsSlotEmpty(0));
            Assert.AreEqual(1, equipment.GetSlot(0).Stack.Count);
        }

        [Test]
        public void HasItem_AggregatesAllContainers()
        {
            main.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 2);
            equipment.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            Assert.IsTrue(group.HasItem(InventoryTestItemDatabase.GeneralItemId, 2));
            Assert.IsTrue(group.HasItem(InventoryTestItemDatabase.EquipmentItemId, 1));
            Assert.AreEqual(2, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }
    }
}
