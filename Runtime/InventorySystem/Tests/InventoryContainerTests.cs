using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryContainerTests
    {
        private InventoryContainer container;

        [SetUp]
        public void SetUp() => container = InventoryTestFixtures.CreateMainContainer();

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
            container = null;
        }

        [Test]
        public void TryAddItem_StackableItem_RespectsMaxStackSize()
        {
            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 12);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(12, result.RequestedCount);
            Assert.AreEqual(12, result.ProcessedCount);
            Assert.AreEqual(0, result.Remainder);
            Assert.AreEqual(12, container.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
            Assert.AreEqual(3, result.ChangedSlotIndices.Length);
        }

        [Test]
        public void TryAddItem_WhenInventoryFull_ReturnsNoSpace()
        {
            InventoryTestFixtures.FillContainer(container, InventoryTestItemDatabase.GeneralItemId, 50);

            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.NoSpace, result.Reason);
        }

        [Test]
        public void TryAddItemToSlot_AddsToSpecificSlot()
        {
            InventoryChangeResult result = container.TryAddItemToSlot(2, InventoryTestItemDatabase.GeneralItemId, 3);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.PrimarySlotIndex);
            Assert.IsFalse(container.IsSlotEmpty(2));
            Assert.AreEqual(3, container.GetSlot(2).Stack.Count);
        }

        [Test]
        public void TryAddItemToSlot_WrongItemInSlot_ReturnsSlotMismatch()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);

            InventoryChangeResult result = container.TryAddItemToSlot(0, InventoryTestItemDatabase.EquipmentItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.SlotMismatch, result.Reason);
        }

        [Test]
        public void TryRemoveItem_RemovesAcrossSlots()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 8);

            InventoryChangeResult result = container.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 6);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(6, result.ProcessedCount);
            Assert.AreEqual(2, container.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
            Assert.IsFalse(result.ItemWasDepleted);
        }

        [Test]
        public void TryRemoveItem_DepletesItem_SetsItemWasDepleted()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 3);

            InventoryChangeResult result = container.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 3);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ItemWasDepleted);
            Assert.IsFalse(container.HasItem(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void TryMoveSlot_MergesSameItemStacks()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 3);
            container.TryAddItemToSlot(1, InventoryTestItemDatabase.GeneralItemId, 2);

            InventoryChangeResult result = container.TryMoveSlot(0, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(InventoryChangeType.Move, result.ChangeType);
            Assert.AreEqual(5, container.GetSlot(1).Stack.Count);
            Assert.IsTrue(container.IsSlotEmpty(0));
        }

        [Test]
        public void TrySwapSlots_SwapsDifferentItems()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);
            container.TryAddItemToSlot(1, InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryChangeResult result = container.TrySwapSlots(0, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(InventoryTestItemDatabase.EquipmentItemId, container.GetSlot(0).Stack.ItemId);
            Assert.AreEqual(InventoryTestItemDatabase.GeneralItemId, container.GetSlot(1).Stack.ItemId);
        }

        [Test]
        public void ClearSlot_RemovesSingleSlot()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 4);

            InventoryChangeResult result = container.ClearSlot(0);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(container.IsSlotEmpty(0));
            Assert.IsTrue(result.ItemWasDepleted);
        }

        [Test]
        public void ClearAll_EmptiesContainer()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 4);
            container.TryAddItemToSlot(3, InventoryTestItemDatabase.EquipmentItemId, 1);

            InventoryChangeResult result = container.ClearAll();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, container.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void NonStackableItem_OccupiesOneSlotPerItem()
        {
            InventoryChangeResult first = container.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);
            InventoryChangeResult second = container.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.Success);
            Assert.AreEqual(2, container.GetItemCount(InventoryTestItemDatabase.EquipmentItemId));
            Assert.AreEqual(1, first.ChangedSlotIndices.Length);
            Assert.AreEqual(1, second.ChangedSlotIndices.Length);
        }

        [Test]
        public void EquipmentContainer_RejectsGeneralItem()
        {
            container.Dispose();
            container = InventoryTestFixtures.CreateEquipmentContainer();

            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.ItemTypeNotAllowed, result.Reason);
        }

        [Test]
        public void EquipmentContainer_AcceptsEquipmentItem()
        {
            container.Dispose();
            container = InventoryTestFixtures.CreateEquipmentContainer();

            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ContainerKind.Equipment, result.Kind);
            Assert.AreEqual("equipment", result.ContainerId);
        }

        [Test]
        public void TryAddItem_SecondAdd_DoesNotSetItemWasAcquired()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 1);

            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.ItemWasAcquired);
        }

        [Test]
        public void TryRemoveItemFromSlot_PartialRemove_KeepsRemainingCount()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 4);

            InventoryChangeResult result = container.TryRemoveItemFromSlot(0, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, container.GetSlot(0).Stack.Count);
            Assert.IsFalse(result.ItemWasDepleted);
        }

        [Test]
        public void HasItem_ReturnsFalseWhenCountInsufficient()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 3);

            Assert.IsTrue(container.HasItem(InventoryTestItemDatabase.GeneralItemId, 3));
            Assert.IsFalse(container.HasItem(InventoryTestItemDatabase.GeneralItemId, 4));
        }
    }
}
