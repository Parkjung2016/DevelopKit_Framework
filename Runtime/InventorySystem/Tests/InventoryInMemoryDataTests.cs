using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryInMemoryDataTests
    {
        private InMemoryItemDatabase itemDatabase;
        private InMemoryRecipeDatabase recipeDatabase;
        private InMemoryLootTableDatabase lootTableDatabase;
        private InventoryContainer container;
        private InventoryGroup group;

        [SetUp]
        public void SetUp()
        {
            itemDatabase = new InMemoryItemDatabase();
            itemDatabase.Register(
                new ItemDefinition(InventoryTestItemDatabase.GeneralItemId, 99, true, ItemType.General),
                displayName: "General",
                tags: new[] { "Material", "Wood" });

            recipeDatabase = new InMemoryRecipeDatabase();
            recipeDatabase.Register(RecipeDefinition.Create(
                "wood_to_plank",
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 3) },
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 1) },
                "Plank"));

            lootTableDatabase = new InMemoryLootTableDatabase();
            lootTableDatabase.Register(new LootTableDefinition(
                "basic_loot",
                new[]
                {
                    new LootEntry
                    {
                        ItemId = InventoryTestItemDatabase.GeneralItemId,
                        MinCount = 2,
                        MaxCount = 2,
                        Weight = 1f
                    }
                }));

            container = new InventoryContainer(10, itemDatabase, InventoryContainerDescriptor.Main());
            group = new InventoryGroup(new InventoryDataProvider(itemDatabase, recipeDatabase, lootTableDatabase));
            group.RegisterContainer(container);
        }

        [TearDown]
        public void TearDown()
        {
            group?.Dispose();
            container?.Dispose();
        }

        [Test]
        public void InMemoryItemDatabase_FindByTag_ReturnsMatchingEntries()
        {
            var results = new List<ItemCatalogEntry>();
            itemDatabase.FindByTag("Wood", results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(InventoryTestItemDatabase.GeneralItemId, results[0].ItemId);
            Assert.AreEqual("General", results[0].DisplayName);
        }

        [Test]
        public void TryCraft_ByRecipeId_UsesInMemoryDatabase()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 5);

            InventoryChangeResult result = group.TryCraft("wood_to_plank");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void TryGrantLoot_ByTableId_UsesInMemoryDatabase()
        {
            InventoryChangeResult result = group.TryGrantLoot("basic_loot", new System.Random(1));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void TryCraft_UnknownRecipeId_ReturnsRecipeNotFound()
        {
            InventoryChangeResult result = group.TryCraft("missing");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.RecipeNotFound, result.Reason);
        }
    }
}
