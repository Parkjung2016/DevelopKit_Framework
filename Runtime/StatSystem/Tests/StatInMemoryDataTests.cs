using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    [TestFixture]
    public sealed class StatInMemoryDataTests
    {
        private InMemoryStatDatabase statDatabase;
        private StatDataProvider dataProvider;
        private StatCollection collection;

        [SetUp]
        public void SetUp()
        {
            statDatabase = new InMemoryStatDatabase();
            statDatabase.Register(new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 100f, 50f));
            statDatabase.Register(new StatDefinition(StatTestDatabase.AtkStatName, "Attack", 0f, 999f, 10f));

            dataProvider = StatDataProvider.FromDatabase(statDatabase);
            collection = StatTestFixtures.CreateCollection(dataProvider.StatDatabase);
        }

        [Test]
        public void InMemoryStatDatabase_TryGetDefinition_ReturnsRegisteredStat()
        {
            Assert.IsTrue(statDatabase.TryGetDefinition(StatTestDatabase.HpStatName, out StatDefinition definition));
            Assert.AreEqual("Health", definition.DisplayName);
            Assert.AreEqual(50f, definition.BaseValue);
        }

        [Test]
        public void InMemoryStatDatabase_TryGetEntry_ReturnsCatalogEntry()
        {
            Assert.IsTrue(statDatabase.TryGetEntry(StatTestDatabase.AtkStatName, out StatCatalogEntry entry));
            Assert.AreEqual(StatTestDatabase.AtkStatName, entry.StatName);
            Assert.AreEqual(10f, entry.Definition.BaseValue);
        }

        [Test]
        public void InMemoryStatDatabase_RegisterRange_LoadsMultipleStats()
        {
            var anotherDatabase = new InMemoryStatDatabase();
            anotherDatabase.RegisterRange(new[]
            {
                new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 100f, 40f),
                new StatDefinition(StatTestDatabase.DefStatName, "Defense", 0f, 500f, 5f)
            });

            Assert.AreEqual(2, anotherDatabase.StatNames.Count);
            Assert.IsTrue(anotherDatabase.TryGetDefinition(StatTestDatabase.DefStatName, out _));
        }

        [Test]
        public void StatDataProvider_ExposesInMemoryDatabase()
        {
            Assert.IsTrue(dataProvider.StatDatabase.TryGetDefinition(StatTestDatabase.HpStatName, out StatDefinition definition));
            Assert.AreEqual(50f, definition.BaseValue);
        }

        [Test]
        public void StatCollection_InitFromInMemoryDatabase_LoadsAllStats()
        {
            Assert.IsTrue(collection.HasStat(StatTestDatabase.HpStatName));
            Assert.IsTrue(collection.HasStat(StatTestDatabase.AtkStatName));
            Assert.AreEqual(50f, collection.GetBaseValue(StatTestDatabase.HpStatName));
        }

        [Test]
        public void StatCollection_InitFromProvider_LoadsAllStats()
        {
            var providerCollection = new StatCollection();
            providerCollection.Init(dataProvider);

            Assert.IsTrue(providerCollection.HasStat(StatTestDatabase.HpStatName));
            Assert.AreEqual(10f, providerCollection.GetBaseValue(StatTestDatabase.AtkStatName));
        }

        [Test]
        public void StatCollection_AddValueModifier_UsesInMemoryDatabase()
        {
            collection.AddValueModifier(StatTestDatabase.HpStatName, "buff", 10f);

            Assert.AreEqual(60f, collection.GetStat(StatTestDatabase.HpStatName).Value);
        }

        [Test]
        public void StatCollection_UnknownStatName_ThrowsKeyNotFound()
        {
            Assert.Throws<KeyNotFoundException>(() => collection.GetStat(StatTestDatabase.UnknownStatName));
        }
    }
}
