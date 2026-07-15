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

        [Test]
        public void TryMoveBetween_PreservesInstanceRuntimeData()
        {
            var factory = new DelegateItemInstanceFactory(_ => new TestItemData());
            main.InstanceFactory = factory;
            equipment.InstanceFactory = factory;
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.EquipmentItemId, 1);
            long instanceId = main.GetSlot(0).Stack.InstanceId;
            Assert.IsTrue(group.ItemInstanceStore.TryGet(instanceId, out TestItemData beforeMove));
            beforeMove.Value = 42;

            InventoryChangeResult result = group.TryMoveBetween("main", 0, "equipment", 0);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(group.ItemInstanceStore.TryGet(instanceId, out TestItemData afterMove));
            Assert.AreSame(beforeMove, afterMove);
            Assert.AreEqual(42, afterMove.Value);
        }

        [Test]
        public void ItemInstanceFactory_SetAfterRegistration_RebindsContainers()
        {
            group.ItemInstanceFactory = new DelegateItemInstanceFactory(_ => new TestItemData());

            InventoryChangeResult result = main.TryAddItemToSlot(
                0,
                InventoryTestItemDatabase.EquipmentItemId,
                1);

            Assert.IsTrue(result.Success);
            long instanceId = main.GetSlot(0).Stack.InstanceId;
            Assert.IsTrue(group.ItemInstanceStore.TryGet(instanceId, out TestItemData _));
        }
        [Test]
        public void TransactionRollback_RestoresRemovedInstanceData()
        {
            group.ItemInstanceFactory = new DelegateItemInstanceFactory(_ => new TestItemData());
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.EquipmentItemId, 1);
            long instanceId = main.GetSlot(0).Stack.InstanceId;
            Assert.IsTrue(group.ItemInstanceStore.TryGet(instanceId, out TestItemData before));
            before.Value = 77;

            using (group.BeginTransaction())
                main.TryRemoveItemFromSlot(0, 1);

            Assert.AreEqual(instanceId, main.GetSlot(0).Stack.InstanceId);
            Assert.IsTrue(group.ItemInstanceStore.TryGet(instanceId, out TestItemData after));
            Assert.AreSame(before, after);
            Assert.AreEqual(77, after.Value);
        }
        private sealed class TestItemData : ItemInstanceData
        {
            public int Value { get; set; }
        }
    }
}
