using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public class Stat
    {
        public delegate void ValueChangeHandler(Stat stat, float current, float prev);

        public event ValueChangeHandler OnValueChanged;

        private readonly Dictionary<object, Stack<float>> modifyValueByKeys = new();
        private readonly Dictionary<object, float> modifyValuePercentByKeys = new();

        public string StatName { get; }
        public string DisplayName { get; }
        public float MinValue { get; }
        public float MaxValue { get; }

        private float baseValue;
        private float _modifiedValue;
        private float _modifiedValuePercent;

        public float Value
        {
            get
            {
                float value = Mathf.Clamp(baseValue + _modifiedValue, MinValue, MaxValue);
                if (_modifiedValuePercent != 0)
                    value *= 1 + _modifiedValuePercent * .01f;

                return MathF.Round(value, 1);
            }
        }

        public bool IsMax => Mathf.Approximately(Value, MaxValue);
        public bool IsMin => Mathf.Approximately(Value, MinValue);

        public float BaseValue
        {
            get
            {
                if (baseValue == 0)
                    return 0;

                return (float)Math.Round(baseValue, 1);
            }
            set
            {
                float prevValue = Value;
                baseValue = Mathf.Clamp(value, MinValue, MaxValue);
                TryInvokeValueChangeEvent(Value, prevValue);
            }
        }

        public Stat(in StatDefinition definition)
        {
            StatName = definition.StatName;
            DisplayName = definition.DisplayName;
            MinValue = definition.MinValue;
            MaxValue = definition.MaxValue;
            baseValue = definition.BaseValue;
        }

        public static Stat CreateFrom(in StatDefinition definition) => new(definition);

        public bool HasModifier() => modifyValueByKeys.Count > 0 || modifyValuePercentByKeys.Count > 0;

        public float GetTotalModifyValue() => MathF.Round(_modifiedValue, 1);

        public float GetTotalModifyValuePercent() => MathF.Round(_modifiedValuePercent, 1);

        public void IncreaseBaseValuePercent(float percent) => BaseValue *= 1 + percent * .01f;

        public void AddModifyValue(object key, float value)
        {
            float prevValue = Value;
            _modifiedValue += value;

            if (!modifyValueByKeys.TryGetValue(key, out Stack<float> stack))
                modifyValueByKeys.Add(key, new Stack<float>(new[] { value }));
            else
                stack.Push(value);

            TryInvokeValueChangeEvent(Value, prevValue);
        }

        public void RemoveModifyValue(object key)
        {
            if (!modifyValueByKeys.TryGetValue(key, out Stack<float> stack) || stack.Count <= 0)
                return;

            float prevValue = Value;
            _modifiedValue -= stack.Pop();
            if (stack.Count <= 0)
                modifyValueByKeys.Remove(key);

            TryInvokeValueChangeEvent(Value, prevValue);
        }

        public void AddModifyValuePercent(object key, float value)
        {
            if (modifyValuePercentByKeys.ContainsKey(key))
                return;

            float prevValue = Value;
            _modifiedValuePercent += value;
            modifyValuePercentByKeys.Add(key, value);
            TryInvokeValueChangeEvent(Value, prevValue);
        }

        public void RemoveModifyValuePercent(object key)
        {
            if (!modifyValuePercentByKeys.Remove(key, out float value))
                return;

            float prevValue = Value;
            _modifiedValuePercent -= value;
            TryInvokeValueChangeEvent(Value, prevValue);
        }

        public void ClearModifier()
        {
            ClearModifyValue();
            ClearModifyValuePercent();
        }

        public void ClearModifyValue()
        {
            float prevValue = Value;
            modifyValueByKeys.Clear();
            _modifiedValue = 0;
            TryInvokeValueChangeEvent(Value, prevValue);
        }

        public void ClearModifyValuePercent()
        {
            float prevValue = Value;
            modifyValuePercentByKeys.Clear();
            _modifiedValuePercent = 0;
            TryInvokeValueChangeEvent(Value, prevValue);
        }

        private void TryInvokeValueChangeEvent(float value, float prevValue)
        {
            if (!Mathf.Approximately(value, prevValue))
                OnValueChanged?.Invoke(this, value, prevValue);
        }
    }
}
