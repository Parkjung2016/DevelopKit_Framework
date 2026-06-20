using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryTransactionTests
    {
        private InventoryContainer container;

        [SetUp]
        public void SetUp()
        {
            container = InventoryTestFixtures.CreateMainContainer();
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 10);
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
            container = null;
        }

        [Test]
        public void Rollback_RestoresPreviousState()
        {
            using (InventoryTransaction.Begin(container))
            {
                container.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 10);
                container.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);
            }

            Assert.AreEqual(10, container.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
            Assert.IsFalse(container.HasItem(InventoryTestItemDatabase.EquipmentItemId));
        }

        [Test]
        public void Commit_KeepsChanges()
        {
            using (var transaction = InventoryTransaction.Begin(container))
            {
                container.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 4);
                transaction.Commit();
            }

            Assert.AreEqual(6, container.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }
    }
}
