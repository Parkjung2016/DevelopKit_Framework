using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    public sealed class ItemCatalogTests
    {
        [TearDown]
        public void TearDown() => ItemCatalog.Clear();

        [Test]
        public void Resolve_UsesExplicitDatabase_WhenProvided()
        {
            var database = new InventoryTestItemDatabase();
            database.Register(new ItemDefinition(1, maxStackSize: 1, isStackable: false, itemType: ItemType.Equipment));

            Assert.IsTrue(ItemCatalog.Resolve(database).TryGetDefinition(1, out _));
        }

        [Test]
        public void Resolve_UsesGlobalCatalog_WhenDatabaseIsNull()
        {
            var database = new InventoryTestItemDatabase();
            database.Register(new ItemDefinition(2, maxStackSize: 5, isStackable: true, itemType: ItemType.Consumable));
            ItemCatalog.Set(database);

            var container = new InventoryContainer(4, null, InventoryContainerDescriptor.Main());
            InventoryChangeResult result = container.TryAddItem(2, 1);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ContainerWithoutCatalog_ReturnsDatabaseNotReady()
        {
            ItemCatalog.Clear();
            var container = InventoryTestFixtures.CreateContainerWithoutDatabase();

            InventoryChangeResult result = container.TryAddItem(1, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.DatabaseNotReady, result.Reason);
        }
    }
}
