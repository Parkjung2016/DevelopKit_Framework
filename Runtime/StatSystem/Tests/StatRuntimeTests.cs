using NUnit.Framework;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    [TestFixture]
    public sealed class StatRuntimeTests
    {
        private Stat hp;

        [SetUp]
        public void SetUp() => hp = StatTestFixtures.CreateHpStat();

        [Test]
        public void Value_ReturnsClampedBaseValue()
        {
            Assert.AreEqual(50f, hp.Value);
            Assert.IsFalse(hp.IsMax);
            Assert.IsFalse(hp.IsMin);
        }

        [Test]
        public void BaseValue_ClampsToMinMax()
        {
            hp.BaseValue = 150f;
            Assert.AreEqual(100f, hp.BaseValue);
            Assert.IsTrue(hp.IsMax);

            hp.BaseValue = -10f;
            Assert.AreEqual(0f, hp.BaseValue);
            Assert.IsTrue(hp.IsMin);
        }

        [Test]
        public void AddModifyValue_UpdatesValueAndCanBeRemoved()
        {
            hp.AddModifyValue("buff", 10f);

            Assert.AreEqual(60f, hp.Value);
            Assert.IsTrue(hp.HasModifier());
            Assert.AreEqual(10f, hp.GetTotalModifyValue());

            hp.RemoveModifyValue("buff");

            Assert.AreEqual(50f, hp.Value);
            Assert.IsFalse(hp.HasModifier());
        }

        [Test]
        public void AddModifyValue_StacksByKey()
        {
            hp.AddModifyValue("buff", 5f);
            hp.AddModifyValue("buff", 3f);

            Assert.AreEqual(58f, hp.Value);

            hp.RemoveModifyValue("buff");
            Assert.AreEqual(55f, hp.Value);

            hp.RemoveModifyValue("buff");
            Assert.AreEqual(50f, hp.Value);
        }

        [Test]
        public void AddModifyValuePercent_AppliesAfterFlatModifier()
        {
            hp.AddModifyValue("flat", 10f);
            hp.AddModifyValuePercent("percent", 50f);

            Assert.AreEqual(90f, hp.Value);
            Assert.AreEqual(50f, hp.GetTotalModifyValuePercent());
        }

        [Test]
        public void AddModifyValuePercent_IgnoresDuplicateKey()
        {
            hp.AddModifyValuePercent("percent", 10f);
            hp.AddModifyValuePercent("percent", 20f);

            Assert.AreEqual(55f, hp.Value);
        }

        [Test]
        public void ClearModifier_RemovesAllModifiers()
        {
            hp.AddModifyValue("flat", 5f);
            hp.AddModifyValuePercent("percent", 10f);

            hp.ClearModifier();

            Assert.AreEqual(50f, hp.Value);
            Assert.IsFalse(hp.HasModifier());
        }

        [Test]
        public void OnValueChanged_FiresWhenValueChanges()
        {
            int invokeCount = 0;
            float current = 0f;
            float previous = 0f;

            hp.OnValueChanged += (stat, value, prev) =>
            {
                invokeCount++;
                current = value;
                previous = prev;
            };

            hp.BaseValue = 60f;

            Assert.AreEqual(1, invokeCount);
            Assert.AreEqual(60f, current);
            Assert.AreEqual(50f, previous);
        }

        [Test]
        public void IncreaseBaseValuePercent_UpdatesBaseValue()
        {
            hp.IncreaseBaseValuePercent(10f);

            Assert.AreEqual(55f, hp.BaseValue);
            Assert.AreEqual(55f, hp.Value);
        }
    }
}
