using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    [TestFixture]
    public sealed class StatCollectionTests
    {
        private StatCollection collection;

        [SetUp]
        public void SetUp() => collection = StatTestFixtures.CreateCollectionFromSharedDatabase();

        [Test]
        public void InitFromSharedDatabase_LoadsAllStats()
        {
            Assert.IsTrue(collection.HasStat(StatTestDatabase.HpStatName));
            Assert.IsTrue(collection.HasStat(StatTestDatabase.AtkStatName));
            Assert.AreEqual(50f, collection.GetBaseValue(StatTestDatabase.HpStatName));
        }

        [Test]
        public void ApplyOverrides_ReplacesBaseValue()
        {
            collection.ApplyOverrides(new[]
            {
                new StatOverrideEntry(
                    new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 100f, 50f),
                    useOverride: true,
                    overrideValue: 80f)
            });

            Assert.AreEqual(80f, collection.GetBaseValue(StatTestDatabase.HpStatName));
        }

        [Test]
        public void InitFromOverrideEntriesOnly_CreatesRequestedStats()
        {
            collection = StatTestFixtures.CreateCollection(new List<StatOverrideEntry>
            {
                new(new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 100f, 30f)),
                new(new StatDefinition(StatTestDatabase.AtkStatName, "Attack", 0f, 999f, 7f))
            });

            Assert.IsTrue(collection.HasStat(StatTestDatabase.HpStatName));
            Assert.IsTrue(collection.HasStat(StatTestDatabase.AtkStatName));
            Assert.IsFalse(collection.HasStat(StatTestDatabase.DefStatName));
            Assert.AreEqual(30f, collection.GetBaseValue(StatTestDatabase.HpStatName));
        }

        [Test]
        public void AddValueModifier_UpdatesRuntimeValue()
        {
            collection.AddValueModifier(StatTestDatabase.HpStatName, "buff", 10f);

            Assert.AreEqual(60f, collection.GetStat(StatTestDatabase.HpStatName).Value);
        }

        [Test]
        public void ClearAllStatModifier_ClearsEveryStat()
        {
            collection.AddValueModifier(StatTestDatabase.HpStatName, "buff", 10f);
            collection.AddValueModifier(StatTestDatabase.AtkStatName, "buff", 5f);

            collection.ClearAllStatModifier();

            Assert.AreEqual(50f, collection.GetStat(StatTestDatabase.HpStatName).Value);
            Assert.AreEqual(10f, collection.GetStat(StatTestDatabase.AtkStatName).Value);
        }

        [Test]
        public void IncreaseAndDecreaseBaseValue_UpdateStoredValue()
        {
            Assert.AreEqual(60f, collection.IncreaseBaseValue(StatTestDatabase.HpStatName, 10f));
            Assert.AreEqual(55f, collection.DecreaseBaseValue(StatTestDatabase.HpStatName, 5f));
        }
    }
}
