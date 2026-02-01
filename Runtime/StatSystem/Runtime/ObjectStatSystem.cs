using System.Collections.Generic;
using System.Linq;
using Skddkkkk.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.StatSystem.Runtime
{
    public class ObjectStatSystem : MonoBehaviour
    {
        [SerializeField] private StatOverrideListSO statOverrideListSO;

        private Dictionary<string, StatSO> statDic;

        public void Init()
        {
            statDic = statOverrideListSO.statOverrides.Select(stat => stat.CreateStat())
                .ToDictionary(stat => stat.StatName, stat => stat);
        }

        #region Get Stat

        public StatSO GetStat(string statName)
        {
            return statDic[statName];
        }

        public StatSO GetStat(StatSO stat)
        {
            SkddkkkkDebug.Assert(stat != null, "Stats : GetStat - stat cannot be null");
            return statDic[stat.StatName];
        }

        #endregion

        #region Has Stat

        public bool HasStat(string statName) => statDic.ContainsKey(statName);

        public bool HasStat(StatSO stat)
        {
            SkddkkkkDebug.Assert(stat != null, "Stats : GetStat - stat cannot be null");
            return statDic.ContainsKey(stat.StatName);
        }

        #endregion

        #region Get / Set Base Value

        public void SetBaseValue(StatSO stat, float value) => GetStat(stat).BaseValue = value;

        public float GetBaseValue(string statName) => GetStat(statName).BaseValue;
        public float GetBaseValue(StatSO stat) => GetStat(stat).BaseValue;

        #endregion

        #region Increase / Decrease Base Value

        public void IncreaseBaseValuePercent(StatSO statSO, float percent) =>
            GetStat(statSO).IncreaseBaseValuePercent(percent);

        public float IncreaseBaseValue(string statName, float value) => GetStat(statName).BaseValue += value;
        public float IncreaseBaseValue(StatSO stat, float value) => GetStat(stat).BaseValue += value;

        public float DecreaseBaseValue(string statName, float value) => GetStat(statName).BaseValue -= value;
        public float DecreaseBaseValue(StatSO stat, float value) => GetStat(stat).BaseValue -= value;

        #endregion

        #region Add / Remove Stat Modifier

        public void AddValueModifier(string statName, object key, float value) =>
            GetStat(statName).AddModifyValue(key, value);

        public void AddValueModifier(StatSO stat, object key, float value) =>
            GetStat(stat).AddModifyValue(key, value);

        public void RemoveValueModifier(string statName, object key) => GetStat(statName).RemoveModifyValue(key);
        public void RemoveValueModifier(StatSO stat, object key) => GetStat(stat).RemoveModifyValue(key);

        public void AddValuePercentModifier(string statName, object key, float value) =>
            GetStat(statName).AddModifyValuePercent(key, value);

        public void AddValuePercentModifier(StatSO stat, object key, float value) =>
            GetStat(stat).AddModifyValuePercent(key, value);

        public void RemoveValuePercentModifier(string statName, object key) =>
            GetStat(statName).RemoveModifyValuePercent(key);

        public void RemoveValuePercentModifier(StatSO stat, object key) =>
            GetStat(stat).RemoveModifyValuePercent(key);

        #endregion

        #region Clear Stat Modifier

        public void ClearAllStatModifier()
        {
            foreach (var stat in statDic.Values)
            {
                stat.ClearModifier();
            }
        }

        public void ClearAllStatValueModifier()
        {
            foreach (var stat in statDic.Values)
            {
                stat.ClearModifyValue();
            }
        }

        public void ClearAllStatValuePercentModifier()
        {
            foreach (var stat in statDic.Values)
            {
                stat.ClearModifyValuePercent();
            }
        }

        #endregion
    }
}