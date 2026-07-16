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

    /// <summary>대상의 Stat 기본값을 변경하거나 활성 Modifier를 적용합니다.</summary>
    [Serializable]
    public sealed class StatAbilityEffect : AbilityEffect
    {
        [SerializeField] private AbilityStatTarget target = AbilityStatTarget.Target;
        [SerializeField] private AbilityStatReference stat = new();
        [SerializeField] private StatEffectMode mode = StatEffectMode.BaseValue;
        [SerializeField] private BaseValueChange baseValueChange = BaseValueChange.Add;
        [SerializeField] private float amount = 0f;
        [SerializeField] private float percent = 0f;

        [NonSerialized] private StatModifierKey modifierKey;

        public StatAbilityEffect()
        {
        }

        public StatAbilityEffect(
            string statName,
            StatEffectMode mode,
            float amount,
            float percent = 0f,
            AbilityStatTarget target = AbilityStatTarget.Target,
            BaseValueChange baseValueChange = BaseValueChange.Add)
        {
            stat = new AbilityStatReference(statName);
            this.mode = mode;
            this.baseValueChange = baseValueChange;
            this.amount = amount;
            this.percent = percent;
            this.target = target;
        }

        public AbilityStatTarget Target => target;
        public AbilityStatReference Stat => stat;
        public StatEffectMode Mode => mode;
        public BaseValueChange BaseValueChange => baseValueChange;
        public float Amount => amount;
        public float Percent => percent;
        public override bool RemoveWhenAbilityEnds => mode == StatEffectMode.Modifier;

        public override bool CanApply(in AbilityContext context, out string failureReason)
        {
            if (stat != null && stat.TryGet(context.GetStats(target), out _))
            {
                failureReason = null;
                return true;
            }

            failureReason = $"Stat '{stat?.StatName ?? "<None>"}' was not found on {target}.";
            return false;
        }

        public override void Apply(in AbilityContext context)
        {
            if (!stat.TryGet(context.GetStats(target), out Stat resolved))
                return;

            if (mode == StatEffectMode.Modifier)
            {
                if (!modifierKey.IsValid)
                    modifierKey = StatModifierKey.CreateRuntime();

                resolved.SetModifier(modifierKey, new StatModifier(amount, percent));
                return;
            }

            switch (baseValueChange)
            {
                case BaseValueChange.Add:
                    resolved.AddBaseValue(amount);
                    break;
                case BaseValueChange.Set:
                    resolved.BaseValue = amount;
                    break;
                case BaseValueChange.AddPercent:
                    resolved.AddBasePercent(percent);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Remove(in AbilityContext context)
        {
            if (mode == StatEffectMode.Modifier &&
                modifierKey.IsValid &&
                stat.TryGet(context.GetStats(target), out Stat resolved))
            {
                resolved.RemoveModifiers(modifierKey);
            }
        }
    }
}