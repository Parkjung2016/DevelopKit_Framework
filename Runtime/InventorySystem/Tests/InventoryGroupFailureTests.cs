using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryGroupFailureTests
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
        public void TryAddItemToContainer_UnknownContainer_ReturnsContainerNotFound()
        {
            InventoryChangeResult result = group.TryAddItemToContainer("missing", InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.ContainerNotFound, result.Reason);
        }

        [Test]
        public void TryMoveBetween_UnknownSourceContainer_ReturnsContainerNotFound()
        {
            InventoryChangeResult result = group.TryMoveBetween("missing", 0, "main", 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.ContainerNotFound, result.Reason);
        }

        [Test]
        public void TryMoveBetween_EmptySourceSlot_ReturnsNoChange()
        {
            InventoryChangeResult result = group.TryMoveBetween("main", 0, "equipment", 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.NoChange, result.Reason);
        }

        [Test]
        public void TryMoveBetween_GeneralItemToEquipmentSlot_ReturnsSlotRuleDenied()
        {
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);

            InventoryChangeResult result = group.TryMoveBetween("main", 0, "equipment", 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.SlotRuleDenied, result.Reason);
        }

        [Test]
        public void TryMoveBetween_MismatchedTargetSlot_ReturnsSlotMismatch()
        {
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);
            equipment.TryAddItemToSlot(0, InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryChangeResult result = group.TryMoveBetween("main", 0, "equipment", 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.SlotMismatch, result.Reason);
        }

        [Test]
        public void TryMoveBetween_SameContainerDifferentItems_PerformsSwap()
        {
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);
            main.TryAddItemToSlot(1, InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryChangeResult result = group.TryMoveBetween("main", 0, "main", 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(InventoryChangeType.Swap, result.ChangeType);
            Assert.AreEqual(InventoryTestItemDatabase.EquipmentItemId, main.GetSlot(0).Stack.ItemId);
            Assert.AreEqual(InventoryTestItemDatabase.GeneralItemId, main.GetSlot(1).Stack.ItemId);
        }

        [Test]
        public void TryAddItemToContainer_AddsToSpecifiedContainer()
        {
            InventoryChangeResult result = group.TryAddItemToContainer("equipment", InventoryTestItemDatabase.EquipmentItemId, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, equipment.GetItemCount(InventoryTestItemDatabase.EquipmentItemId));
            Assert.AreEqual(0, main.GetItemCount(InventoryTestItemDatabase.EquipmentItemId));
        }
    }
}
