using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Stat System")]
    public sealed class ObjectStatSystem : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private StatDatabaseSO statDatabase = null;
        [SerializeField] private StatOverrideListSO overrides = null;

        private readonly StatCollection statCollection = new();
        private readonly List<StatOverrideEntry> overrideBuffer = new();

        public StatCollection StatCollection => statCollection;
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            IStatCatalog catalog = statDatabase != null ? statDatabase : StatCatalog.Current;
            overrides?.CopyEntriesTo(overrideBuffer);
            if (overrides == null)
                overrideBuffer.Clear();

            statCollection.Initialize(catalog, overrideBuffer);
            IsInitialized = true;
        }

        public void Initialize(IStatCatalog catalog, IReadOnlyList<StatOverrideEntry> statOverrides = null)
        {
            statCollection.Initialize(catalog, statOverrides);
            IsInitialized = true;
        }

        public void Initialize(IReadOnlyList<StatOverrideEntry> statOverrides)
        {
            statCollection.Initialize(statOverrides);
            IsInitialized = true;
        }

        public Stat GetStat(string statName) => statCollection.GetStat(statName);

        public Stat GetStat(StatSO stat) =>
            statCollection.GetStat(GetStatName(stat));

        public bool TryGetStat(string statName, out Stat stat) =>
            statCollection.TryGetStat(statName, out stat);

        public bool TryGetStat(StatSO statAsset, out Stat stat)
        {
            stat = null;
            return statAsset != null && statCollection.TryGetStat(statAsset.StatName, out stat);
        }

        public bool HasStat(string statName) => statCollection.HasStat(statName);

        public bool HasStat(StatSO stat) =>
            stat != null && statCollection.HasStat(stat.StatName);

        public float GetBaseValue(string statName) => statCollection.GetBaseValue(statName);

        public void SetBaseValue(string statName, float value) =>
            statCollection.SetBaseValue(statName, value);

        public float AddBaseValue(string statName, float amount) =>
            statCollection.AddBaseValue(statName, amount);

        public void SetModifier(string statName, StatModifierKey key, in StatModifier modifier) =>
            statCollection.SetModifier(statName, key, modifier);

        public void SetFlatModifier(string statName, StatModifierKey key, float amount) =>
            statCollection.SetFlatModifier(statName, key, amount);

        public void SetPercentModifier(string statName, StatModifierKey key, float percent) =>
            statCollection.SetPercentModifier(statName, key, percent);

        public bool RemoveFlatModifier(string statName, StatModifierKey key) =>
            statCollection.RemoveFlatModifier(statName, key);

        public bool RemovePercentModifier(string statName, StatModifierKey key) =>
            statCollection.RemovePercentModifier(statName, key);

        public bool RemoveModifiers(string statName, StatModifierKey key) =>
            statCollection.RemoveModifiers(statName, key);

        public void ClearModifiers() => statCollection.ClearModifiers();

        public StatCollectionSnapshot CaptureSnapshot(
            StatCollectionSnapshot destination = null,
            bool includePersistentModifiers = true) =>
            statCollection.CaptureSnapshot(destination, includePersistentModifiers);

        public int RestoreSnapshot(StatCollectionSnapshot snapshot, bool ignoreMissingStats = true) =>
            statCollection.RestoreSnapshot(snapshot, ignoreMissingStats);

        private static string GetStatName(StatSO stat)
        {
            if (stat == null)
                throw new ArgumentNullException(nameof(stat));

            return stat.StatName;
        }
    }
}