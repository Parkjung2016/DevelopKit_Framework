using System.Collections.Generic;
using System.Linq;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public class ObjectStatSystem : MonoBehaviour
    {
        [SerializeField] private StatSetupSO statSetup;
        [SerializeField] private StatOverrideListSO statOverrideListSO;

        private readonly StatCollection collection = new();

        public void Init()
        {
            if (statSetup != null)
                Init(statSetup.CreateDataProvider(), statOverrideListSO);
            else if (statOverrideListSO != null)
                Init(statOverrideListSO);
            else
                CDebug.LogWarning("ObjectStatSystem : StatSetupSO or StatOverrideListSO is not assigned.");
        }

        public void Init(StatSetupSO setup, StatOverrideListSO overrideList = null)
        {
            if (setup == null)
            {
                CDebug.LogWarning("ObjectStatSystem : StatSetupSO is null.");
                return;
            }

            Init(setup.CreateDataProvider(), overrideList);
        }

        public void Init(IStatDataProvider dataProvider, StatOverrideListSO overrideList = null) =>
            collection.Init(dataProvider, ToOverrideEntries(overrideList));

        public void Init(IStatCatalog statDatabase, StatOverrideListSO overrideList = null) =>
            collection.Init(statDatabase, ToOverrideEntries(overrideList));

        public void Init(StatOverrideListSO overrideList) =>
            collection.Init(ToOverrideEntries(overrideList));

        public void Init(IStatCatalog statDatabase, IReadOnlyList<StatOverrideEntry> overrides) =>
            collection.Init(statDatabase, overrides);

        public void Init(IReadOnlyList<StatOverrideEntry> overrides) =>
            collection.Init(overrides);

        public Stat GetStat(string statName) => collection.GetStat(statName);

        public Stat GetStat(StatSO stat)
        {
            CDebug.Assert(stat != null, "Stats : GetStat - stat cannot be null");
            return collection.GetStat(stat.StatName);
        }

        public Stat GetStat(in StatDefinition definition) => collection.GetStat(definition);

        public bool HasStat(string statName) => collection.HasStat(statName);

        public bool HasStat(StatSO stat)
        {
            CDebug.Assert(stat != null, "Stats : GetStat - stat cannot be null");
            return collection.HasStat(stat.StatName);
        }

        public bool HasStat(in StatDefinition definition) => collection.HasStat(definition);

        public void SetBaseValue(StatSO stat, float value) => collection.SetBaseValue(stat.StatName, value);

        public float GetBaseValue(string statName) => collection.GetBaseValue(statName);

        public float GetBaseValue(StatSO stat) => collection.GetBaseValue(stat.StatName);

        public void IncreaseBaseValuePercent(StatSO statSO, float percent) =>
            collection.IncreaseBaseValuePercent(statSO.StatName, percent);

        public float IncreaseBaseValue(string statName, float value) => collection.IncreaseBaseValue(statName, value);

        public float IncreaseBaseValue(StatSO stat, float value) => collection.IncreaseBaseValue(stat.StatName, value);

        public float DecreaseBaseValue(string statName, float value) => collection.DecreaseBaseValue(statName, value);

        public float DecreaseBaseValue(StatSO stat, float value) => collection.DecreaseBaseValue(stat.StatName, value);

        public void AddValueModifier(string statName, object key, float value) =>
            collection.AddValueModifier(statName, key, value);

        public void AddValueModifier(StatSO stat, object key, float value) =>
            collection.AddValueModifier(stat.StatName, key, value);

        public void RemoveValueModifier(string statName, object key) =>
            collection.RemoveValueModifier(statName, key);

        public void RemoveValueModifier(StatSO stat, object key) =>
            collection.RemoveValueModifier(stat.StatName, key);

        public void AddValuePercentModifier(string statName, object key, float value) =>
            collection.AddValuePercentModifier(statName, key, value);

        public void AddValuePercentModifier(StatSO stat, object key, float value) =>
            collection.AddValuePercentModifier(stat.StatName, key, value);

        public void RemoveValuePercentModifier(string statName, object key) =>
            collection.RemoveValuePercentModifier(statName, key);

        public void RemoveValuePercentModifier(StatSO stat, object key) =>
            collection.RemoveValuePercentModifier(stat.StatName, key);

        public void ClearAllStatModifier() => collection.ClearAllStatModifier();

        public void ClearAllStatValueModifier() => collection.ClearAllStatValueModifier();

        public void ClearAllStatValuePercentModifier() => collection.ClearAllStatValuePercentModifier();

        private static IReadOnlyList<StatOverrideEntry> ToOverrideEntries(StatOverrideListSO overrideList)
        {
            if (overrideList?.statOverrides == null)
                return null;

            return overrideList.statOverrides
                .Where(entry => entry != null)
                .Select(entry => entry.ToEntry())
                .ToList();
        }
    }
}
