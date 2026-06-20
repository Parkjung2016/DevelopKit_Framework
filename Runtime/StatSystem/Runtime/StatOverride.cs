using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [Serializable]
    public class StatOverride
    {
        [SerializeField] private StatSO stat;
        [SerializeField] private bool isUseOverride;
        [SerializeField] private float overrideValue;

        public StatOverride(StatSO stat) => this.stat = stat;

        public Stat CreateStat()
        {
            if (stat == null)
                return null;

            Stat newStat = stat.CreateRuntime();
            if (isUseOverride)
                newStat.BaseValue = overrideValue;

            return newStat;
        }

        public StatOverrideEntry ToEntry() =>
            stat == null
                ? default
                : new StatOverrideEntry(stat.ToDefinition(), isUseOverride, overrideValue);
    }
}
