using System;
using System.Collections;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 한 객체가 소유한 런타임 스탯을 관리합니다.
    /// </summary>
    public sealed class StatCollection : IReadOnlyCollection<Stat>
    {
        private readonly Dictionary<string, Stat> stats = new(StringComparer.Ordinal);

        public int Count => stats.Count;

        public void Initialize(IStatCatalog catalog, IReadOnlyList<StatOverrideEntry> overrides = null)
        {
            stats.Clear();

            if (catalog != null)
            {
                IReadOnlyList<StatDefinition> definitions = catalog.Definitions;
                for (int i = 0; i < definitions.Count; i++)
                    AddOrReplace(definitions[i]);
            }

            ApplyOverrides(overrides);
        }

        public void Initialize(IReadOnlyList<StatOverrideEntry> overrides)
        {
            stats.Clear();
            ApplyOverrides(overrides);
        }

        public void ApplyOverrides(IReadOnlyList<StatOverrideEntry> overrides)
        {
            if (overrides == null)
                return;

            for (int i = 0; i < overrides.Count; i++)
            {
                StatOverrideEntry entry = overrides[i];
                if (entry.IsValid)
                    stats[entry.StatName] = entry.CreateStat();
            }
        }

        public Stat GetStat(string statName)
        {
            if (TryGetStat(statName, out Stat stat))
                return stat;

            throw new KeyNotFoundException($"Stat '{statName}' was not found.");
        }

        public bool TryGetStat(string statName, out Stat stat)
        {
            if (!string.IsNullOrEmpty(statName))
                return stats.TryGetValue(statName, out stat);

            stat = null;
            return false;
        }

        public bool HasStat(string statName) => TryGetStat(statName, out _);

        public float GetBaseValue(string statName) => GetStat(statName).BaseValue;

        public void SetBaseValue(string statName, float value) =>
            GetStat(statName).BaseValue = value;

        public float AddBaseValue(string statName, float amount)
        {
            Stat stat = GetStat(statName);
            stat.AddBaseValue(amount);
            return stat.BaseValue;
        }

        public void SetModifier(string statName, object key, in StatModifier modifier) =>
            GetStat(statName).SetModifier(key, modifier);

        public void SetFlatModifier(string statName, object key, float amount) =>
            GetStat(statName).SetFlatModifier(key, amount);

        public void SetPercentModifier(string statName, object key, float percent) =>
            GetStat(statName).SetPercentModifier(key, percent);

        public bool RemoveFlatModifier(string statName, object key) =>
            GetStat(statName).RemoveFlatModifier(key);

        public bool RemovePercentModifier(string statName, object key) =>
            GetStat(statName).RemovePercentModifier(key);

        public bool RemoveModifiers(string statName, object key) =>
            GetStat(statName).RemoveModifiers(key);

        public void ClearModifiers()
        {
            foreach (Stat stat in stats.Values)
                stat.ClearModifiers();
        }

        public StatCollectionSnapshot CaptureSnapshot(StatCollectionSnapshot destination = null)
        {
            destination ??= new StatCollectionSnapshot();
            destination.Capture(this);
            return destination;
        }

        public int RestoreSnapshot(StatCollectionSnapshot snapshot, bool ignoreMissingStats = true)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            int restoredCount = 0;
            IReadOnlyList<StatValueSnapshot> values = snapshot.Stats;
            for (int i = 0; i < values.Count; i++)
            {
                StatValueSnapshot savedStat = values[i];
                if (TryGetStat(savedStat.StatName, out Stat stat))
                {
                    stat.BaseValue = savedStat.BaseValue;
                    restoredCount++;
                    continue;
                }

                if (!ignoreMissingStats)
                    throw new KeyNotFoundException($"Stat '{savedStat.StatName}' was not found.");
            }

            return restoredCount;
        }

        public IEnumerator<Stat> GetEnumerator() => stats.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void AddOrReplace(in StatDefinition definition)
        {
            if (definition.IsValid)
                stats[definition.StatName] = new Stat(definition);
        }
    }
}