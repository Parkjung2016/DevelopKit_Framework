using System;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    /// <summary>Stat 에셋 또는 이름으로 런타임 Stat을 찾습니다.</summary>
    [Serializable]
    public sealed class AbilityStatReference
    {
        [SerializeField] private StatSO stat = null;
        [SerializeField] private string statName = null;

        public AbilityStatReference()
        {
        }

        public AbilityStatReference(string statName)
        {
            this.statName = statName;
        }

        public AbilityStatReference(StatSO stat)
        {
            this.stat = stat;
        }

        public string StatName => stat != null ? stat.StatName : statName?.Trim();

        public bool TryGet(StatCollection statCollection, out Stat result)
        {
            result = null;
            string resolvedName = StatName;
            return statCollection != null &&
                   !string.IsNullOrWhiteSpace(resolvedName) &&
                   statCollection.TryGetStat(resolvedName, out result);
        }
    }
}
