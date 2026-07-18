using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Stat System")]
    public sealed class ObjectStatSystem : MonoBehaviour
    {
        private readonly StatCollection stats = new();

        public StatCollection Stats => stats;
        public bool IsInitialized { get; private set; }

        public void Initialize() => Initialize(StatCatalog.Current);

        public void Initialize(
            IStatCatalog catalog,
            IReadOnlyList<StatOverrideEntry> overrides = null)
        {
            stats.Initialize(catalog, overrides);
            IsInitialized = true;
        }

        public void Initialize(IReadOnlyList<StatDefinition> definitions)
        {
            stats.Initialize(definitions);
            IsInitialized = true;
        }

        public Stat GetStat(StatId id) => stats.GetStat(id);

        public bool TryGetStat(StatId id, out Stat stat) =>
            stats.TryGetStat(id, out stat);

        public bool HasStat(StatId id) => stats.HasStat(id);

        public float GetBaseValue(StatId id) => stats.GetBaseValue(id);

        public void SetBaseValue(StatId id, float value) =>
            stats.SetBaseValue(id, value);

        public float AddBaseValue(StatId id, float amount) =>
            stats.AddBaseValue(id, amount);

        public void SetModifier(StatId id, StatModifierKey key, in StatModifier modifier) =>
            stats.SetModifier(id, key, modifier);

        public void SetFlatModifier(StatId id, StatModifierKey key, float amount) =>
            stats.SetFlatModifier(id, key, amount);

        public void SetPercentModifier(StatId id, StatModifierKey key, float percent) =>
            stats.SetPercentModifier(id, key, percent);

        public bool RemoveFlatModifier(StatId id, StatModifierKey key) =>
            stats.RemoveFlatModifier(id, key);

        public bool RemovePercentModifier(StatId id, StatModifierKey key) =>
            stats.RemovePercentModifier(id, key);

        public bool RemoveModifiers(StatId id, StatModifierKey key) =>
            stats.RemoveModifiers(id, key);

        public void ClearModifiers() => stats.ClearModifiers();

        public StatCollectionSnapshot CaptureSnapshot(
            StatCollectionSnapshot destination = null,
            bool includePersistentModifiers = true) =>
            stats.CaptureSnapshot(destination, includePersistentModifiers);

        public int RestoreSnapshot(
            StatCollectionSnapshot snapshot,
            bool ignoreMissingStats = true) =>
            stats.RestoreSnapshot(snapshot, ignoreMissingStats);
    }
}
