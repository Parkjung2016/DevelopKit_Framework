using System;
using UnityEngine;

namespace Code.Runtime.Core.StatSystem
{
    [Serializable]
    public class StatOverride
    {
        [SerializeField] private StatSO stat;
        [SerializeField] private bool isUseOverride;

        [SerializeField] private float overrideValue;
        
        public StatOverride(StatSO stat) => this.stat = stat;

        public StatSO CreateStat()
        {
            StatSO newStat = stat.Clone();
            if (isUseOverride)
                newStat.BaseValue = overrideValue;
            return newStat;
        }
    }
}