using System;
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
        public void BaseValue_IsClamped()
        {
            hp.BaseValue = 150f;
            Assert.AreEqual(100f, hp.BaseValue);
            Assert.IsTrue(hp.IsMax);

            hp.BaseValue = -10f;
            Assert.AreEqual(0f, hp.BaseValue);
            Assert.IsTrue(hp.IsMin);
        }

        [Test]
        public void SetFlatModifier_ReplacesSameKey()
        {
            hp.SetFlatModifier("equipment", 5f);
            hp.SetFlatModifier("equipment", 12f);

            Assert.AreEqual(62f, hp.Value);
            Assert.AreEqual(12f, hp.FlatModifierTotal);
            Assert.AreEqual(1, hp.ModifierCount);
        }

        [Test]
        public void FlatAndPercentModifiers_AreCombinedAndClamped()
        {
            hp.SetFlatModifier("flat", 10f);
            hp.SetPercentModifier("percent", 50f);

            Assert.AreEqual(90f, hp.Value);

            hp.SetPercentModifier("percent", 200f);
            Assert.AreEqual(100f, hp.Value);
        }

        [Test]
        public void SetModifier_StoresFlatAndPercentUnderOneSource()
        {
            hp.SetModifier("equipment", new StatModifier(flat: 10f, percent: 20f));

            Assert.AreEqual(72f, hp.Value);
            Assert.AreEqual(1, hp.ModifierCount);
            Assert.IsTrue(hp.TryGetModifier("equipment", out StatModifier modifier));
            Assert.AreEqual(10f, modifier.Flat);
            Assert.AreEqual(20f, modifier.Percent);
        }

        [Test]
        public void SettingEmptyModifier_RemovesSource()
        {
            hp.SetFlatModifier("equipment", 10f);
            hp.SetModifier("equipment", default);

            Assert.AreEqual(0, hp.ModifierCount);
            Assert.AreEqual(50f, hp.Value);
        }
        [Test]
        public void RemoveModifiers_RemovesBothTypesForKey()
        {
            hp.SetFlatModifier("buff", 10f);
            hp.SetPercentModifier("buff", 20f);

            Assert.IsTrue(hp.RemoveModifiers("buff"));
            Assert.AreEqual(50f, hp.Value);
            Assert.IsFalse(hp.HasModifiers);
        }

        [Test]
        public void ModifierKey_CannotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() => hp.SetFlatModifier(null, 10f));
            Assert.Throws<ArgumentNullException>(() => hp.RemovePercentModifier(null));
        }

        [Test]
        public void OnValueChanged_FiresOnlyWhenFinalValueChanges()
        {
            int callCount = 0;
            float current = 0f;
            float previous = 0f;
            hp.OnValueChanged += (_, value, oldValue) =>
            {
                callCount++;
                current = value;
                previous = oldValue;
            };

            hp.BaseValue = 50f;
            hp.SetFlatModifier("buff", 10f);
            hp.SetFlatModifier("buff", 10f);

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(60f, current);
            Assert.AreEqual(50f, previous);
        }

        [Test]
        public void ClearModifiers_RestoresBaseValue()
        {
            hp.SetFlatModifier("flat", 5f);
            hp.SetPercentModifier("percent", 10f);

            hp.ClearModifiers();

            Assert.AreEqual(50f, hp.Value);
            Assert.AreEqual(0, hp.ModifierCount);
        }
    }
}