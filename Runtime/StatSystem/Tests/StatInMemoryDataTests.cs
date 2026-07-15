using NUnit.Framework;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    [TestFixture]
    public sealed class StatInMemoryDataTests
    {
        private InMemoryStatDatabase database;

        [SetUp]
        public void SetUp()
        {
            database = new InMemoryStatDatabase();
            database.Register(new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 100f, 50f));
            database.Register(new StatDefinition(StatTestDatabase.AtkStatName, "Attack", 0f, 999f, 10f));
        }

        [Test]
        public void TryGetDefinition_ReturnsRegisteredStat()
        {
            Assert.IsTrue(database.TryGetDefinition(StatTestDatabase.HpStatName, out StatDefinition definition));
            Assert.AreEqual("Health", definition.DisplayName);
            Assert.AreEqual(50f, definition.BaseValue);
        }

        [Test]
        public void Register_DuplicateNameReplacesDefinitionWithoutChangingCount()
        {
            Assert.IsFalse(database.Register(
                new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 200f, 75f)));

            Assert.AreEqual(2, database.Definitions.Count);
            Assert.IsTrue(database.TryGetDefinition(StatTestDatabase.HpStatName, out StatDefinition definition));
            Assert.AreEqual(75f, definition.BaseValue);
            Assert.AreEqual(200f, definition.MaxValue);
        }

        [Test]
        public void RegisterRange_LoadsDefinitions()
        {
            database.RegisterRange(new[]
            {
                new StatDefinition(StatTestDatabase.DefStatName, "Defense", 0f, 500f, 5f)
            });

            Assert.AreEqual(3, database.Definitions.Count);
            Assert.IsTrue(database.TryGetDefinition(StatTestDatabase.DefStatName, out _));
        }

        [Test]
        public void Collection_CanUseInMemoryCatalog()
        {
            var collection = new StatCollection();
            collection.Initialize(database);

            Assert.AreEqual(2, collection.Count);
            Assert.AreEqual(10f, collection.GetBaseValue(StatTestDatabase.AtkStatName));
        }
    }
}