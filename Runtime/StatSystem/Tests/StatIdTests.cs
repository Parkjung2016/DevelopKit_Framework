using NUnit.Framework;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    [TestFixture]
    public sealed class StatIdTests
    {
        private enum TestStat
        {
            Health,
            [StatIdValue("Combat.Attack")]
            Attack
        }

        [Test]
        public void StringId_TrimsAndUsesOrdinalEquality()
        {
            StatId id = new("  Health  ");

            Assert.AreEqual("Health", id.Value);
            Assert.AreEqual(new StatId("Health"), id);
            Assert.AreNotEqual(new StatId("health"), id);
        }

        [Test]
        public void EnumId_CanAccessAndModifyStat()
        {
            var definitions = new[]
            {
                new StatDefinition(
                    StatId.From(TestStat.Health),
                    minValue: 0f,
                    maxValue: 100f,
                    baseValue: 50f)
            };
            var stats = new StatCollection();
            stats.Initialize(definitions);

            stats.SetFlatModifier(TestStat.Health, "equipment", 10f);

            Assert.AreEqual(60f, stats.GetStat(TestStat.Health).Value);
            Assert.IsTrue(stats.HasStat(TestStat.Health));
        }

        [Test]
        public void EnumConversion_ReusesSameId()
        {
            StatId first = StatId.From(TestStat.Attack);
            StatId second = StatId.From(TestStat.Attack);

            Assert.AreEqual(first, second);
            Assert.AreEqual("Combat.Attack", first.Value);
        }
    }
}
