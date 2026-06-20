using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryValidationTests
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
        public void TryAddItem_InvalidItemId_ReturnsInvalidItemId()
        {
            InventoryChangeResult result = container.TryAddItem(0, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.InvalidItemId, result.Reason);
        }

        [Test]
        public void TryAddItem_InvalidCount_ReturnsInvalidCount()
        {
            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.InvalidCount, result.Reason);
        }

        [Test]
        public void TryAddItem_UnknownDefinition_ReturnsDefinitionNotFound()
        {
            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.UnknownItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.DefinitionNotFound, result.Reason);
        }

        [Test]
        public void TryAddItemToSlot_InvalidSlotIndex_ReturnsInvalidSlotIndex()
        {
            InventoryChangeResult result = container.TryAddItemToSlot(-1, InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.InvalidSlotIndex, result.Reason);
        }

        [Test]
        public void TryAddItem_WithoutDatabase_ReturnsDatabaseNotReady()
        {
            container.Dispose();
            container = InventoryTestFixtures.CreateContainerWithoutDatabase();

            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.DatabaseNotReady, result.Reason);
        }

        [Test]
        public void TryRemoveItem_WhenEmpty_ReturnsNoChange()
        {
            InventoryChangeResult result = container.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.NoChange, result.Reason);
        }

        [Test]
        public void TryMoveSlot_SameIndex_ReturnsNoChange()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);

            InventoryChangeResult result = container.TryMoveSlot(0, 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.NoChange, result.Reason);
        }

        [Test]
        public void ClearSlot_WhenEmpty_ReturnsNoChange()
        {
            InventoryChangeResult result = container.ClearSlot(0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.NoChange, result.Reason);
        }
    }
}
