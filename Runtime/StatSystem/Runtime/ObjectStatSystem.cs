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
        [SerializeField] private bool initializeOnAwake = true;

        private readonly StatCollection stats = new();
        private readonly List<StatOverrideEntry> overrideBuffer = new();

        public StatCollection Stats => stats;
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (initializeOnAwake)
                Initialize();
        }

        public void Initialize()
        {
            IStatCatalog catalog = statDatabase != null ? statDatabase : StatCatalog.Current;
            overrides?.CopyEntriesTo(overrideBuffer);
            if (overrides == null)
                overrideBuffer.Clear();

            stats.Initialize(catalog, overrideBuffer);
            IsInitialized = true;
        }

        public void Initialize(IStatCatalog catalog, IReadOnlyList<StatOverrideEntry> statOverrides = null)
        {
            stats.Initialize(catalog, statOverrides);
            IsInitialized = true;
        }

        public void Initialize(IReadOnlyList<StatOverrideEntry> statOverrides)
        {
            stats.Initialize(statOverrides);
            IsInitialized = true;
        }

        public Stat GetStat(string statName) => stats.GetStat(statName);

        public Stat GetStat(StatSO stat) =>
            stats.GetStat(GetStatName(stat));

        public bool TryGetStat(string statName, out Stat stat) =>
            stats.TryGetStat(statName, out stat);

        public bool TryGetStat(StatSO statAsset, out Stat stat)
        {
            stat = null;
            return statAsset != null && stats.TryGetStat(statAsset.StatName, out stat);
        }

        public bool HasStat(string statName) => stats.HasStat(statName);

        public bool HasStat(StatSO stat) =>
            stat != null && stats.HasStat(stat.StatName);

        public float GetBaseValue(string statName) => stats.GetBaseValue(statName);

        public void SetBaseValue(string statName, float value) =>
            stats.SetBaseValue(statName, value);

        public float AddBaseValue(string statName, float amount) =>
            stats.AddBaseValue(statName, amount);

        public void SetModifier(string statName, object key, in StatModifier modifier) =>
            stats.SetModifier(statName, key, modifier);

        public void SetFlatModifier(string statName, object key, float amount) =>
            stats.SetFlatModifier(statName, key, amount);

        public void SetPercentModifier(string statName, object key, float percent) =>
            stats.SetPercentModifier(statName, key, percent);

        public bool RemoveFlatModifier(string statName, object key) =>
            stats.RemoveFlatModifier(statName, key);

        public bool RemovePercentModifier(string statName, object key) =>
            stats.RemovePercentModifier(statName, key);

        public bool RemoveModifiers(string statName, object key) =>
            stats.RemoveModifiers(statName, key);

        public void ClearModifiers() => stats.ClearModifiers();

        public StatCollectionSnapshot CaptureSnapshot(StatCollectionSnapshot destination = null) =>
            stats.CaptureSnapshot(destination);

        public int RestoreSnapshot(StatCollectionSnapshot snapshot, bool ignoreMissingStats = true) =>
            stats.RestoreSnapshot(snapshot, ignoreMissingStats);

        private static string GetStatName(StatSO stat)
        {
            if (stat == null)
                throw new ArgumentNullException(nameof(stat));

            return stat.StatName;
        }
    }
}