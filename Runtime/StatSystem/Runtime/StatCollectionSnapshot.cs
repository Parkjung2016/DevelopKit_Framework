using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 저장할 수 있는 스탯 기본값 하나를 나타냅니다.
    /// 장비나 버프 같은 임시 수정자는 포함하지 않습니다.
    /// </summary>
    [Serializable]
    public struct StatValueSnapshot
    {
        [SerializeField] private string statName;
        [SerializeField] private float baseValue;

        public string StatName => statName;
        public float BaseValue => baseValue;

        public StatValueSnapshot(string statName, float baseValue)
        {
            this.statName = statName;
            this.baseValue = baseValue;
        }
    }

    /// <summary>
    /// StatCollection의 저장 및 복원용 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class StatCollectionSnapshot
    {
        [SerializeField] private List<StatValueSnapshot> stats = new();

        public IReadOnlyList<StatValueSnapshot> Stats => stats;

        internal void Capture(StatCollection source)
        {
            stats.Clear();
            if (stats.Capacity < source.Count)
                stats.Capacity = source.Count;

            foreach (Stat stat in source)
                stats.Add(new StatValueSnapshot(stat.StatName, stat.BaseValue));
        }
    }
}