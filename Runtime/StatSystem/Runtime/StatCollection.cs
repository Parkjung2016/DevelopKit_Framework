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
        private readonly Dictionary<StatId, Stat> stats = new();

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

        public void Initialize(IReadOnlyList<StatDefinition> definitions)
        {
            stats.Clear();
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
                AddOrReplace(definitions[i]);
        }
        public void ApplyOverrides(IReadOnlyList<StatOverrideEntry> overrides)
        {
            if (overrides == null)
                return;

            for (int i = 0; i < overrides.Count; i++)
            {
                StatOverrideEntry entry = overrides[i];
                if (entry.IsValid)
                    stats[entry.Id] = entry.CreateStat();
            }
        }

        public Stat GetStat(StatId id)
        {
            if (TryGetStat(id, out Stat stat))
                return stat;

            throw new KeyNotFoundException($"Stat '{id.Value}' was not found.");
        }

        public bool TryGetStat(StatId id, out Stat stat)
        {
            if (id.IsValid)
                return stats.TryGetValue(id, out stat);

            stat = null;
            return false;
        }

        public bool HasStat(StatId id) => TryGetStat(id, out _);

        public float GetBaseValue(StatId id) => GetStat(id).BaseValue;

        public void SetBaseValue(StatId id, float value) =>
            GetStat(id).BaseValue = value;

        public float AddBaseValue(StatId id, float amount)
        {
            Stat stat = GetStat(id);
            stat.AddBaseValue(amount);
            return stat.BaseValue;
        }

        public void SetModifier(StatId id, StatModifierKey key, in StatModifier modifier) =>
            GetStat(id).SetModifier(key, modifier);

        public void SetFlatModifier(StatId id, StatModifierKey key, float amount) =>
            GetStat(id).SetFlatModifier(key, amount);

        public void SetPercentModifier(StatId id, StatModifierKey key, float percent) =>
            GetStat(id).SetPercentModifier(key, percent);

        public bool RemoveFlatModifier(StatId id, StatModifierKey key) =>
            GetStat(id).RemoveFlatModifier(key);

        public bool RemovePercentModifier(StatId id, StatModifierKey key) =>
            GetStat(id).RemovePercentModifier(key);

        public bool RemoveModifiers(StatId id, StatModifierKey key) =>
            GetStat(id).RemoveModifiers(key);

        public void ClearModifiers()
        {
            foreach (Stat stat in stats.Values)
                stat.ClearModifiers();
        }

        public StatCollectionSnapshot CaptureSnapshot(
            StatCollectionSnapshot destination = null,
            bool includePersistentModifiers = true)
        {
            destination ??= new StatCollectionSnapshot();
            destination.Capture(this, includePersistentModifiers);
            return destination;
        }

        public int RestoreSnapshot(StatCollectionSnapshot snapshot, bool ignoreMissingStats = true)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            foreach (Stat stat in stats.Values)
                stat.ClearPersistentModifiers();

            int restoredCount = 0;
            IReadOnlyList<StatValueSnapshot> values = snapshot.Stats;
            for (int i = 0; i < values.Count; i++)
            {
                StatValueSnapshot savedStat = values[i];
                if (TryGetStat(savedStat.Id, out Stat stat))
                {
                    stat.BaseValue = savedStat.BaseValue;
                    restoredCount++;
                    continue;
                }

                if (!ignoreMissingStats)
                    throw new KeyNotFoundException($"Stat '{savedStat.Id.Value}' was not found.");
            }

            IReadOnlyList<StatModifierSnapshot> modifiers = snapshot.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifierSnapshot savedModifier = modifiers[i];
                if (TryGetStat(savedModifier.Id, out Stat stat))
                {
                    if (!string.IsNullOrWhiteSpace(savedModifier.Key))
                    {
                        stat.SetModifier(
                            StatModifierKey.Persistent(savedModifier.Key),
                            savedModifier.Modifier);
                    }

                    continue;
                }

                if (!ignoreMissingStats)
                    throw new KeyNotFoundException($"Stat '{savedModifier.Id.Value}' was not found.");
            }

            return restoredCount;
        }

        public IEnumerator<Stat> GetEnumerator() => stats.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void AddOrReplace(in StatDefinition definition)
        {
            if (definition.IsValid)
                stats[definition.Id] = new Stat(definition);
        }
    }
}