using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    [TestFixture]
    public sealed class StatCollectionTests
    {
        private StatCollection collection;

        [SetUp]
        public void SetUp() => collection = StatTestFixtures.CreateCollectionFromSharedDatabase();

        [Test]
        public void Initialize_LoadsAllDefinitions()
        {
            Assert.AreEqual(3, collection.Count);
            Assert.IsTrue(collection.HasStat(StatTestDatabase.HpStatName));
            Assert.AreEqual(50f, collection.GetBaseValue(StatTestDatabase.HpStatName));
        }

        [Test]
        public void InitializeAgain_RemovesOldRuntimeState()
        {
            collection.SetFlatModifier(StatTestDatabase.HpStatName, "buff", 10f);
            collection.Initialize(StatTestDatabase.Shared);

            Assert.AreEqual(50f, collection.GetStat(StatTestDatabase.HpStatName).Value);
            Assert.IsFalse(collection.GetStat(StatTestDatabase.HpStatName).HasModifiers);
        }

        [Test]
        public void ApplyOverrides_ReplacesDefinition()
        {
            collection.ApplyOverrides(new[]
            {
                new StatOverrideEntry(
                    new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 200f, 50f),
                    overrideBaseValue: true,
                    baseValue: 80f)
            });

            Assert.AreEqual(80f, collection.GetBaseValue(StatTestDatabase.HpStatName));
            Assert.AreEqual(200f, collection.GetStat(StatTestDatabase.HpStatName).MaxValue);
        }

        [Test]
        public void TryGetStat_ReturnsFalseForUnknownName()
        {
            Assert.IsFalse(collection.TryGetStat(StatTestDatabase.UnknownStatName, out _));
            Assert.Throws<KeyNotFoundException>(() => collection.GetStat(StatTestDatabase.UnknownStatName));
        }

        [Test]
        public void ModifierOperations_UpdateRequestedStat()
        {
            collection.SetFlatModifier(StatTestDatabase.HpStatName, "equipment", 10f);
            collection.SetPercentModifier(StatTestDatabase.HpStatName, "buff", 20f);

            Assert.That(
                collection.GetStat(StatTestDatabase.HpStatName).Value,
                Is.EqualTo(72f).Within(0.0001f));

            collection.RemoveFlatModifier(StatTestDatabase.HpStatName, "equipment");
            Assert.That(
                collection.GetStat(StatTestDatabase.HpStatName).Value,
                Is.EqualTo(60f).Within(0.0001f));
        }

        [Test]
        public void Snapshot_RestoresBaseValuesWithoutTemporaryModifiers()
        {
            collection.SetBaseValue(StatTestDatabase.HpStatName, 70f);
            StatModifierKey runtimeKey = StatModifierKey.CreateRuntime();
            collection.SetFlatModifier(StatTestDatabase.HpStatName, runtimeKey, 10f);
            StatCollectionSnapshot snapshot = collection.CaptureSnapshot();

            collection.SetBaseValue(StatTestDatabase.HpStatName, 20f);
            int restoredCount = collection.RestoreSnapshot(snapshot);

            Assert.AreEqual(collection.Count, restoredCount);
            Assert.AreEqual(70f, collection.GetBaseValue(StatTestDatabase.HpStatName));
            Assert.AreEqual(80f, collection.GetStat(StatTestDatabase.HpStatName).Value);
            Assert.IsTrue(collection.GetStat(StatTestDatabase.HpStatName)
                .TryGetModifier(runtimeKey, out _));
        }

        [Test]
        public void CaptureSnapshot_CanReuseDestination()
        {
            var destination = new StatCollectionSnapshot();

            StatCollectionSnapshot result = collection.CaptureSnapshot(destination);

            Assert.AreSame(destination, result);
            Assert.AreEqual(collection.Count, result.Stats.Count);
        }

        [Test]
        public void Snapshot_RoundTripsThroughUnityJson()
        {
            collection.SetBaseValue(StatTestDatabase.HpStatName, 75f);
            StatCollectionSnapshot source = collection.CaptureSnapshot();

            string json = JsonUtility.ToJson(source);
            StatCollectionSnapshot restored = JsonUtility.FromJson<StatCollectionSnapshot>(json);

            collection.SetBaseValue(StatTestDatabase.HpStatName, 10f);
            collection.RestoreSnapshot(restored);

            Assert.AreEqual(75f, collection.GetBaseValue(StatTestDatabase.HpStatName));
        }

        [Test]
        public void PersistentModifier_RoundTripsThroughUnityJson()
        {
            collection.SetFlatModifier(StatTestDatabase.HpStatName, "equipment.weapon", 15f);
            StatCollectionSnapshot source = collection.CaptureSnapshot();

            string json = JsonUtility.ToJson(source);
            StatCollectionSnapshot restored = JsonUtility.FromJson<StatCollectionSnapshot>(json);

            collection.SetFlatModifier(StatTestDatabase.HpStatName, "equipment.weapon", 2f);
            collection.RestoreSnapshot(restored);

            Stat hp = collection.GetStat(StatTestDatabase.HpStatName);
            Assert.IsTrue(hp.TryGetModifier("equipment.weapon", out StatModifier modifier));
            Assert.AreEqual(15f, modifier.Flat);
            Assert.AreEqual(65f, hp.Value);
        }

        [Test]
        public void SnapshotWithoutModifiers_ClearsExistingPersistentModifiers()
        {
            collection.SetFlatModifier(StatTestDatabase.HpStatName, "equipment.weapon", 15f);
            StatCollectionSnapshot snapshot = collection.CaptureSnapshot(
                includePersistentModifiers: false);

            collection.RestoreSnapshot(snapshot);

            Assert.IsFalse(collection.GetStat(StatTestDatabase.HpStatName)
                .TryGetModifier("equipment.weapon", out _));
        }

        [Test]
        public void Enumeration_VisitsEveryStat()
        {
            int count = 0;
            foreach (Stat _ in collection)
                count++;

            Assert.AreEqual(collection.Count, count);
        }
    }
}
