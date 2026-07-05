using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryDataPipelineTests
    {
        private ItemDefinitionSO generalItem;
        private ItemDefinitionSO heavyItem;
        private ItemDatabaseSO database;
        private InventoryContainer container;

        [SetUp]
        public void SetUp()
        {
            generalItem = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            generalItem.ItemId = InventoryTestItemDatabase.GeneralItemId;
            generalItem.DisplayName = "General";
            generalItem.ItemType = (ItemType)InventoryTestValues.GeneralType;
            generalItem.Weight = 0f;

            heavyItem = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            heavyItem.ItemId = 4000;
            heavyItem.DisplayName = "Heavy";
            heavyItem.Weight = 10f;

            database = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            database.Items = new[] { generalItem, heavyItem };
            database.RebuildCache();

            container = new InventoryContainer(
                10,
                database,
                new InventoryContainerDescriptor(
                    "main",
                    (ContainerKind)InventoryTestValues.MainKind,
                    AnySlotRule.Instance,
                    new WeightCapacityRule(25f)));
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(generalItem);
            Object.DestroyImmediate(heavyItem);
        }

        [Test]
        public void ItemDatabaseSO_TryGetDefinition_ReturnsCachedDefinition()
        {
            Assert.IsTrue(database.TryGetDefinition(InventoryTestItemDatabase.GeneralItemId, out ItemDefinition definition));
            Assert.AreEqual((ItemType)InventoryTestValues.GeneralType, definition.ItemType);
        }

        [Test]
        public void ItemDatabaseSO_FindByTag_ReturnsMatchingItems()
        {
            generalItem.Tags = new[] { "Material", "Wood" };
            database.RebuildCache();

            var results = new List<ItemCatalogEntry>();
            database.FindByTag("Wood", results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(InventoryTestItemDatabase.GeneralItemId, results[0].ItemId);
        }

        [Test]
        public void WeightCapacityRule_BlocksOverweightAdd()
        {
            InventoryChangeResult result = container.TryAddItem(4000, 3);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.WeightLimitExceeded, result.Reason);
        }

        [Test]
        public void GetTotalWeight_SumsOccupiedSlots()
        {
            container.TryAddItem(4000, 2);

            Assert.AreEqual(20f, container.GetTotalWeight());
            Assert.AreEqual(20f, InventoryQueries.GetTotalWeight(container, database));
        }

        [Test]
        public void ExportSaveData_IncludesVersion()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 2);

            InventoryContainerSaveData saveData = InventorySerializer.Export(container);

            Assert.AreEqual(InventorySaveVersions.Current, saveData.Version);
        }

        [Test]
        public void LootRoller_GrantsItemsToGroup()
        {
            var table = ScriptableObject.CreateInstance<LootTableSO>();
            table.Entries = new[]
            {
                new LootEntry
                {
                    ItemId = InventoryTestItemDatabase.GeneralItemId,
                    MinCount = 2,
                    MaxCount = 2,
                    Weight = 1f
                }
            };
            table.RollCount = 1;

            InventoryGroup group = InventoryTestFixtures.CreateGroup(container);
            InventoryChangeResult result = LootRoller.TryGrantLoot(group, table.ToDefinition(), RandomSources.Deterministic(1));

            Object.DestroyImmediate(table);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
            group.Dispose();
        }

        [Test]
        public void LootRoller_WithoutDuplicateRolls_PicksEachEntryAtMostOnce()
        {
            var table = ScriptableObject.CreateInstance<LootTableSO>();
            table.Entries = new[]
            {
                new LootEntry { ItemId = InventoryTestItemDatabase.GeneralItemId, MinCount = 1, MaxCount = 1, Weight = 1f },
                new LootEntry { ItemId = InventoryTestItemDatabase.EquipmentItemId, MinCount = 1, MaxCount = 1, Weight = 1f }
            };
            table.RollCount = 2;
            table.AllowDuplicateRolls = false;

            ItemStack[] loot = LootRoller.Roll(table.ToDefinition(), InventoryTestItemDatabase.Shared, RandomSources.Deterministic(1));

            Object.DestroyImmediate(table);

            Assert.AreEqual(2, loot.Length);
            Assert.AreNotEqual(loot[0].ItemId, loot[1].ItemId);
        }

        [Test]
        public void InventoryConfigSO_NormalizeCapacityLimits_ClampsMaxOccupiedSlotsToSlotCount()
        {
            var config = ScriptableObject.CreateInstance<InventoryConfigSO>();
            config.SlotCount = 8;
            config.MaxOccupiedSlots = 20;
            config.CapacityRuleMode = InventoryCapacityRuleMode.SlotCount;

            config.NormalizeCapacityLimits();

            Assert.AreEqual(8, config.MaxOccupiedSlots);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void InventoryConfigSO_CreateDescriptor_BuildsInlineRules()
        {
            var config = ScriptableObject.CreateInstance<InventoryConfigSO>();
            config.ContainerId = "bag";
            config.Kind = (ContainerKind)InventoryTestValues.EquipmentKind;
            config.SlotCount = 8;
            config.SlotRuleMode = InventorySlotRuleMode.ItemType;
            config.AllowedSlotTypes = new[] { (ItemType)InventoryTestValues.EquipmentType };
            config.CapacityRuleMode = InventoryCapacityRuleMode.Weight;
            config.MaxWeight = 25f;

            InventoryContainerDescriptor descriptor = config.CreateDescriptor();

            Object.DestroyImmediate(config);

            Assert.AreEqual("bag", descriptor.ContainerId);
            Assert.AreEqual((ContainerKind)InventoryTestValues.EquipmentKind, descriptor.Kind);
            Assert.IsInstanceOf<ItemTypeSlotRule>(descriptor.SlotRule);
            Assert.IsInstanceOf<WeightCapacityRule>(descriptor.CapacityRule);
            Assert.AreEqual(25f, ((WeightCapacityRule)descriptor.CapacityRule).MaxWeight);
        }

        [Test]
        public void RecipeCraft_UsesRecipeSO()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 5);
            InventoryGroup group = InventoryTestFixtures.CreateGroup(container);

            var recipe = ScriptableObject.CreateInstance<RecipeSO>();
            recipe.Costs = new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 3) };
            recipe.Rewards = new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 1) };

            InventoryChangeResult result = group.TryCraft(recipe);

            Object.DestroyImmediate(recipe);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
            group.Dispose();
        }
    }
}
