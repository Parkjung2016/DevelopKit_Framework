using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 기본값과 수정자를 조합해 최종값을 계산하는 런타임 스탯입니다.
    /// </summary>
    public sealed class Stat
    {
        private readonly Dictionary<StatModifierKey, StatModifier> modifiers = new();
        private readonly List<StatModifierKey> modifierKeyBuffer = new();

        private float baseValue;
        private float flatModifierTotal;
        private float percentModifierTotal;
        private float value;

        public event Action<Stat, float, float> OnValueChanged;

        public string StatName { get; }
        public string DisplayName { get; }
        public Sprite StatIcon { get; }
        public float MinValue { get; }
        public float MaxValue { get; }

        public float BaseValue
        {
            get => baseValue;
            set
            {
                float clamped = Mathf.Clamp(value, MinValue, MaxValue);
                if (Mathf.Approximately(baseValue, clamped))
                    return;

                baseValue = clamped;
                Recalculate();
            }
        }

        public float Value => value;
        public float FlatModifierTotal => flatModifierTotal;
        public float PercentModifierTotal => percentModifierTotal;
        public int ModifierCount => modifiers.Count;
        public bool HasModifiers => modifiers.Count > 0;
        public bool IsMax => Mathf.Approximately(value, MaxValue);
        public bool IsMin => Mathf.Approximately(value, MinValue);

        public Stat(in StatDefinition definition)
        {
            if (!definition.IsValid)
                throw new ArgumentException("A stat name is required.", nameof(definition));

            StatName = definition.StatName;
            DisplayName = definition.DisplayName;
            StatIcon = definition.StatIcon;
            MinValue = definition.MinValue;
            MaxValue = definition.MaxValue;
            baseValue = Mathf.Clamp(definition.BaseValue, MinValue, MaxValue);
            value = baseValue;
        }

        public void AddBaseValue(float amount) => BaseValue += amount;

        public void AddBasePercent(float percent) => BaseValue *= 1f + percent * 0.01f;

        public void SetModifier(StatModifierKey key, in StatModifier modifier)
        {
            ValidateKey(key);
            modifiers.TryGetValue(key, out StatModifier previous);

            if (Mathf.Approximately(previous.Flat, modifier.Flat) &&
                Mathf.Approximately(previous.Percent, modifier.Percent))
                return;

            if (modifier.IsEmpty)
                modifiers.Remove(key);
            else
                modifiers[key] = modifier;

            flatModifierTotal += modifier.Flat - previous.Flat;
            percentModifierTotal += modifier.Percent - previous.Percent;
            Recalculate();
        }

        public void SetFlatModifier(StatModifierKey key, float amount)
        {
            ValidateKey(key);
            modifiers.TryGetValue(key, out StatModifier modifier);
            SetModifier(key, modifier.WithFlat(amount));
        }

        public void SetPercentModifier(StatModifierKey key, float percent)
        {
            ValidateKey(key);
            modifiers.TryGetValue(key, out StatModifier modifier);
            SetModifier(key, modifier.WithPercent(percent));
        }

        public bool TryGetModifier(StatModifierKey key, out StatModifier modifier)
        {
            ValidateKey(key);
            return modifiers.TryGetValue(key, out modifier);
        }

        public bool RemoveFlatModifier(StatModifierKey key)
        {
            ValidateKey(key);
            if (!modifiers.TryGetValue(key, out StatModifier modifier) ||
                Mathf.Approximately(modifier.Flat, 0f))
                return false;

            SetModifier(key, modifier.WithFlat(0f));
            return true;
        }

        public bool RemovePercentModifier(StatModifierKey key)
        {
            ValidateKey(key);
            if (!modifiers.TryGetValue(key, out StatModifier modifier) ||
                Mathf.Approximately(modifier.Percent, 0f))
                return false;

            SetModifier(key, modifier.WithPercent(0f));
            return true;
        }

        public bool RemoveModifiers(StatModifierKey key)
        {
            ValidateKey(key);
            if (!modifiers.Remove(key, out StatModifier modifier))
                return false;

            flatModifierTotal -= modifier.Flat;
            percentModifierTotal -= modifier.Percent;
            Recalculate();
            return true;
        }

        public void ClearModifiers()
        {
            if (modifiers.Count == 0)
                return;

            modifiers.Clear();
            flatModifierTotal = 0f;
            percentModifierTotal = 0f;
            Recalculate();
        }

        internal void CapturePersistentModifiers(List<StatModifierSnapshot> destination)
        {
            foreach (KeyValuePair<StatModifierKey, StatModifier> pair in modifiers)
            {
                if (pair.Key.IsPersistent)
                    destination.Add(new StatModifierSnapshot(StatName, pair.Key.PersistentId, pair.Value));
            }
        }

        internal void ClearPersistentModifiers()
        {
            modifierKeyBuffer.Clear();
            foreach (StatModifierKey key in modifiers.Keys)
            {
                if (key.IsPersistent)
                    modifierKeyBuffer.Add(key);
            }

            if (modifierKeyBuffer.Count == 0)
                return;

            for (int i = 0; i < modifierKeyBuffer.Count; i++)
            {
                StatModifierKey key = modifierKeyBuffer[i];
                StatModifier modifier = modifiers[key];
                modifiers.Remove(key);
                flatModifierTotal -= modifier.Flat;
                percentModifierTotal -= modifier.Percent;
            }

            modifierKeyBuffer.Clear();
            Recalculate();
        }

        private void Recalculate()
        {
            float previous = value;
            float multiplier = 1f + percentModifierTotal * 0.01f;
            value = Mathf.Clamp((baseValue + flatModifierTotal) * multiplier, MinValue, MaxValue);

            if (!Mathf.Approximately(value, previous))
                OnValueChanged?.Invoke(this, value, previous);
        }

        private static void ValidateKey(StatModifierKey key)
        {
            if (!key.IsValid)
                throw new ArgumentException("A valid modifier key is required.", nameof(key));
        }
    }
}