using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Tests;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryGroupInstanceMoveTests
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
        public void TryMoveBetween_PreservesInstanceId_ForEquipmentItem()
        {
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.EquipmentItemId, 1);
            long instanceId = main.GetSlot(0).Stack.InstanceId;
            Assert.Greater(instanceId, 0);

            InventoryChangeResult result = group.TryMoveBetween("main", 0, "equipment", 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(instanceId, equipment.GetSlot(0).Stack.InstanceId);
        }
    }
}
