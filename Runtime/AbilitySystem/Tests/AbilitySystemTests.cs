using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime.Tests
{
    [TestFixture]
    public sealed class AbilitySystemTests
    {
        private const string Health = "Health";
        private const string Attack = "Attack";

        private GameObject sourceObject;
        private GameObject targetObject;
        private ObjectStatSystem sourceStats;
        private ObjectStatSystem targetStats;

        [SetUp]
        public void SetUp()
        {
            var database = new InMemoryStatDatabase();
            database.Register(new StatDefinition(Health, minValue: 0f, maxValue: 100f, baseValue: 50f));
            database.Register(new StatDefinition(Attack, minValue: 0f, maxValue: 500f, baseValue: 10f));

            sourceObject = new GameObject("Ability Source");
            targetObject = new GameObject("Ability Target");
            sourceStats = sourceObject.AddComponent<ObjectStatSystem>();
            targetStats = targetObject.AddComponent<ObjectStatSystem>();
            sourceStats.Initialize(database);
            targetStats.Initialize(database);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }

        [Test]
        public void StatEffect_AddsTargetBaseValue()
        {
            var effect = new StatAbilityEffect(Health, StatEffectMode.BaseValue, -15f);
            var context = new AbilityContext(null, null, null, sourceStats.Stats, targetStats.Stats);

            Assert.IsTrue(effect.CanApply(context, out _));
            effect.Apply(context);

            Assert.AreEqual(35f, targetStats.GetBaseValue(Health));
            Assert.AreEqual(50f, sourceStats.GetBaseValue(Health));
        }

        [Test]
        public void Modifier_IsRemovedWhenAbilityEnds()
        {
            TestAbility ability = ScriptableObject.CreateInstance<TestAbility>();
            var effect = new StatAbilityEffect(
                Attack,
                StatEffectMode.Modifier,
                amount: 10f,
                percent: 50f,
                target: AbilityStatTarget.Self);
            SetList(ability, "effects", new List<AbilityEffect> { effect });
            var context = new AbilityContext(null, ability, null, sourceStats.Stats, targetStats.Stats);

            ability.ActivateInternal(context, null);
            Assert.AreEqual(30f, sourceStats.GetStat(Attack).Value);

            ability.EndInternal();
            Assert.AreEqual(10f, sourceStats.GetStat(Attack).Value);
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void Costs_AreCombinedBeforeActivationAndPaidOnce()
        {
            TestAbility ability = ScriptableObject.CreateInstance<TestAbility>();
            SetList(
                ability,
                "statCosts",
                new List<AbilityStatCost>
                {
                    new(Health, 20f),
                    new(Health, 20f)
                });
            var context = new AbilityContext(null, ability, null, sourceStats.Stats, targetStats.Stats);

            Assert.IsTrue(ability.CanStart(context, out _));
            ability.ActivateInternal(context, null);

            Assert.AreEqual(10f, sourceStats.GetBaseValue(Health));
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void Cost_CombinesAmountAndBaseValuePercent()
        {
            TestAbility ability = ScriptableObject.CreateInstance<TestAbility>();
            SetList(
                ability,
                "statCosts",
                new List<AbilityStatCost>
                {
                    new(Health, amount: 5f, percent: 10f),
                    new(Health, amount: 0f, percent: 10f)
                });
            var context = new AbilityContext(null, ability, null, sourceStats.Stats, targetStats.Stats);

            Assert.IsTrue(ability.CanStart(context, out _));
            ability.ActivateInternal(context, null);

            Assert.AreEqual(35f, sourceStats.GetBaseValue(Health));
            Object.DestroyImmediate(ability);
        }
        [Test]
        public void Costs_BlockActivationWhenCombinedAmountIsTooHigh()
        {
            TestAbility ability = ScriptableObject.CreateInstance<TestAbility>();
            SetList(
                ability,
                "statCosts",
                new List<AbilityStatCost>
                {
                    new(Health, 30f),
                    new(Health, 30f)
                });
            var context = new AbilityContext(null, ability, null, sourceStats.Stats, targetStats.Stats);

            Assert.IsFalse(ability.CanStart(context, out string reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(50f, sourceStats.GetBaseValue(Health));
            Object.DestroyImmediate(ability);
        }

        private static void SetList<T>(AbilitySO ability, string fieldName, List<T> value)
        {
            FieldInfo field = typeof(AbilitySO).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(ability, value);
        }

        private sealed class TestAbility : AbilitySO
        {
        }
    }
}
