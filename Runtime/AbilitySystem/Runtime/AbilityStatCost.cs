using System;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    public enum StatCostPercentBase
    {
        BaseValue,
        MaxValue
    }

    /// <summary>Ability 활성화 전에 확인하고 차감하는 스탯 비용입니다.</summary>
    [Serializable]
    public sealed class AbilityStatCost
    {
        [SerializeField] private AbilityStatReference stat = new();
        [SerializeField, Min(0f)] private float amount = 0f;
        [SerializeField, Min(0f)] private float percent = 0f;
        [SerializeField] private StatCostPercentBase percentBase = StatCostPercentBase.BaseValue;

        public AbilityStatCost()
        {
        }

        public AbilityStatCost(
            string statName,
            float amount,
            float percent = 0f,
            StatCostPercentBase percentBase = StatCostPercentBase.BaseValue)
        {
            stat = new AbilityStatReference(statName);
            this.amount = Math.Max(0f, amount);
            this.percent = Math.Max(0f, percent);
            this.percentBase = percentBase;
        }

        public AbilityStatReference Stat => stat;
        public float Amount => amount;
        public float Percent => percent;
        public StatCostPercentBase PercentBase => percentBase;
        public bool HasCost => amount > 0f || percent > 0f;

        public float CalculateCost(Stat resolvedStat)
        {
            if (resolvedStat == null)
                throw new ArgumentNullException(nameof(resolvedStat));

            float percentValue = percentBase == StatCostPercentBase.MaxValue
                ? resolvedStat.MaxValue
                : resolvedStat.BaseValue;
            return Math.Max(0f, amount + percentValue * percent * 0.01f);
        }

        internal bool TryGetStat(StatCollection statCollection, out Stat result)
        {
            result = null;
            return stat != null && stat.TryGet(statCollection, out result);
        }

    }
}