using System.Collections.Generic;
using System.Linq;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public sealed class StatCollection
    {
        private Dictionary<string, Stat> statDic = new();

        public void Init(IStatDataProvider dataProvider, IReadOnlyList<StatOverrideEntry> overrides = null) =>
            Init(dataProvider?.StatDatabase, overrides);

        public void Init(IStatCatalog statDatabase, IReadOnlyList<StatOverrideEntry> overrides = null)
        {
            statDic = new Dictionary<string, Stat>();

            if (statDatabase != null)
            {
                foreach (string statName in statDatabase.StatNames)
                {
                    if (statDatabase.TryGetDefinition(statName, out StatDefinition definition))
                        statDic[statName] = Stat.CreateFrom(definition);
                }
            }

            ApplyOverrides(overrides);
        }

        public void Init(IReadOnlyList<StatOverrideEntry> overrides)
        {
            statDic = overrides
                .Where(entry => !string.IsNullOrEmpty(entry.StatName))
                .Select(entry => entry.CreateStat())
                .ToDictionary(stat => stat.StatName, stat => stat);
        }

        public void ApplyOverrides(IReadOnlyList<StatOverrideEntry> overrides)
        {
            if (overrides == null)
                return;

            foreach (StatOverrideEntry entry in overrides)
            {
                if (string.IsNullOrEmpty(entry.StatName))
                    continue;

                statDic[entry.StatName] = entry.CreateStat();
            }
        }

        public Stat GetStat(string statName) => statDic[statName];

        public Stat GetStat(in StatDefinition definition) => GetStat(definition.StatName);

        public bool HasStat(string statName) => statDic.ContainsKey(statName);

        public bool HasStat(in StatDefinition definition) => HasStat(definition.StatName);

        public void SetBaseValue(string statName, float value) => GetStat(statName).BaseValue = value;

        public float GetBaseValue(string statName) => GetStat(statName).BaseValue;

        public void IncreaseBaseValuePercent(string statName, float percent) =>
            GetStat(statName).IncreaseBaseValuePercent(percent);

        public float IncreaseBaseValue(string statName, float value) => GetStat(statName).BaseValue += value;

        public float DecreaseBaseValue(string statName, float value) => GetStat(statName).BaseValue -= value;

        public void AddValueModifier(string statName, object key, float value) =>
            GetStat(statName).AddModifyValue(key, value);

        public void RemoveValueModifier(string statName, object key) =>
            GetStat(statName).RemoveModifyValue(key);

        public void AddValuePercentModifier(string statName, object key, float value) =>
            GetStat(statName).AddModifyValuePercent(key, value);

        public void RemoveValuePercentModifier(string statName, object key) =>
            GetStat(statName).RemoveModifyValuePercent(key);

        public void ClearAllStatModifier()
        {
            foreach (Stat stat in statDic.Values)
                stat.ClearModifier();
        }

        public void ClearAllStatValueModifier()
        {
            foreach (Stat stat in statDic.Values)
                stat.ClearModifyValue();
        }

        public void ClearAllStatValuePercentModifier()
        {
            foreach (Stat stat in statDic.Values)
                stat.ClearModifyValuePercent();
        }
    }
}
