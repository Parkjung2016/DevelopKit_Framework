using System;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    public enum StatEffectMode
    {
        BaseValue,
        Modifier
    }

    public enum BaseValueChange
    {
        Add,
        Set,
        AddPercent
    }

    /// <summary>대상의 스탯 기본값을 변경하거나 활성 Modifier를 적용합니다.</summary>
    [Serializable]
    public sealed class StatAbilityEffect : AbilityEffect
    {
        [SerializeField] private AbilityStatTarget target = AbilityStatTarget.Target;
        [SerializeField] private StatId statId = default;
        [SerializeField] private StatEffectMode mode = StatEffectMode.BaseValue;
        [SerializeField] private BaseValueChange baseValueChange = BaseValueChange.Add;
        [SerializeField] private float amount = 0f;
        [SerializeField] private float percent = 0f;

        [NonSerialized] private StatModifierKey modifierKey;

        public StatAbilityEffect()
        {
        }

        public StatAbilityEffect(
            StatId statId,
            StatEffectMode mode,
            float amount,
            float percent = 0f,
            AbilityStatTarget target = AbilityStatTarget.Target,
            BaseValueChange baseValueChange = BaseValueChange.Add)
        {
            this.statId = statId;
            this.mode = mode;
            this.baseValueChange = baseValueChange;
            this.amount = amount;
            this.percent = percent;
            this.target = target;
        }

        public AbilityStatTarget Target => target;
        public StatId StatId => statId;
        public StatEffectMode Mode => mode;
        public BaseValueChange BaseValueChange => baseValueChange;
        public float Amount => amount;
        public float Percent => percent;
        public override bool RemoveWhenAbilityEnds => mode == StatEffectMode.Modifier;

        public override bool CanApply(in AbilityContext context, out string failureReason)
        {
            if (TryGetStat(context, out _))
            {
                failureReason = null;
                return true;
            }

            failureReason = $"Stat '{statId.Value}' was not found on {target}.";
            return false;
        }

        public override void Apply(in AbilityContext context)
        {
            if (!TryGetStat(context, out Stat stat))
                return;

            if (mode == StatEffectMode.Modifier)
            {
                if (!modifierKey.IsValid)
                    modifierKey = StatModifierKey.CreateRuntime();

                stat.SetModifier(modifierKey, new StatModifier(amount, percent));
                return;
            }

            switch (baseValueChange)
            {
                case BaseValueChange.Add:
                    stat.AddBaseValue(amount);
                    break;
                case BaseValueChange.Set:
                    stat.BaseValue = amount;
                    break;
                case BaseValueChange.AddPercent:
                    stat.AddBasePercent(percent);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Remove(in AbilityContext context)
        {
            if (mode == StatEffectMode.Modifier &&
                modifierKey.IsValid &&
                TryGetStat(context, out Stat stat))
            {
                stat.RemoveModifiers(modifierKey);
            }
        }

        private bool TryGetStat(in AbilityContext context, out Stat stat)
        {
            StatCollection stats = context.GetStats(target);
            stat = null;
            return stats != null && statId.IsValid && stats.TryGetStat(statId, out stat);
        }
    }
}
