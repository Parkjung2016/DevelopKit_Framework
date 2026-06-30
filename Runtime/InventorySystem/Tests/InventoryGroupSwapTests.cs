using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Tests;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryGroupSwapTests
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
        public void TryMoveBetween_SameUniqueItemOccupiedSlot_SwapsInstances()
        {
            main.TryAddItemToSlot(1, InventoryTestItemDatabase.EquipmentItemId, 1);
            equipment.TryAddItemToSlot(0, InventoryTestItemDatabase.EquipmentItemId, 1);
            long equippedInstance = equipment.GetSlot(0).Stack.InstanceId;
            long inventoryInstance = main.GetSlot(1).Stack.InstanceId;

            InventoryChangeResult result = group.TryMoveBetween("main", 1, "equipment", 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(inventoryInstance, equipment.GetSlot(0).Stack.InstanceId);
            Assert.AreEqual(equippedInstance, main.GetSlot(1).Stack.InstanceId);
        }
    }
}
