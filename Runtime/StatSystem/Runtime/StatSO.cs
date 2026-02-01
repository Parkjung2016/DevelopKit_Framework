using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_Stat", menuName = "SO/StatSystem/Stat")]
    public class StatSO : ScriptableObject
    {
        public delegate void ValueChangeHandler(StatSO stat, float current, float prev);

        public event ValueChangeHandler OnValueChanged;

        private readonly Dictionary<object, Stack<float>> modifyValueByKeys = new();
        private readonly Dictionary<object, float> modifyValuePercentByKeys = new();

        [field: SerializeField, Delayed] public string StatName { get; private set; }

        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public float MinValue { get; private set; }
        [field: SerializeField] public float MaxValue { get; private set; }
        [SerializeField] private float baseValue;

        private float _modifiedValue = 0;
        private float _modifiedValuePercent = 0f;

        public float Value
        {
            get
            {
                float value = Mathf.Clamp(baseValue + _modifiedValue, MinValue, MaxValue);
                if (_modifiedValuePercent != 0)
                {
                    value *= (1 + _modifiedValuePercent * .01f);
                }

                float roundedValue = MathF.Round(value, 1);
                return roundedValue;
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
                float roundedValue = (float)Math.Round(baseValue, 1);
                return roundedValue;
            }
            set
            {
                float prevValue = Value;
                baseValue = Mathf.Clamp(value, MinValue, MaxValue);
                TryInvokeValueChangeEvent(Value, prevValue);
            }
        }

        public bool HasModifier() => modifyValueByKeys.Count > 0 || modifyValuePercentByKeys.Count > 0;

        public float GetTotalModifyValue()
        {
            float modifyValue = _modifiedValue;
            modifyValue = MathF.Round(modifyValue, 1);
            return modifyValue;
        }

        public float GetTotalModifyValuePercent()
        {
            float modifyValuePercent = _modifiedValuePercent;
            modifyValuePercent = MathF.Round(modifyValuePercent, 1);
            return modifyValuePercent;
        }

        public void IncreaseBaseValuePercent(float percent)
        {
            BaseValue *= (1 + percent * .01f);
        }

        public void AddModifyValue(object key, float value)
        {
            float prevValue = Value;
            _modifiedValue += value;

            if (!modifyValueByKeys.TryGetValue(key, out var stack))
                modifyValueByKeys.Add(key, new Stack<float>(new[] { value }));
            else
                stack.Push(value);

            TryInvokeValueChangeEvent(Value, prevValue);
        }

        public void RemoveModifyValue(object key)
        {
            if (modifyValueByKeys.TryGetValue(key, out var stack))
            {
                if (stack.Count <= 0) return;

                float prevValue = Value;
                _modifiedValue -= stack.Pop();
                if (stack.Count <= 0)
                    modifyValueByKeys.Remove(key);
                TryInvokeValueChangeEvent(Value, prevValue);
            }
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
            if (modifyValuePercentByKeys.Remove(key, out float value))
            {
                float prevValue = Value;
                _modifiedValuePercent -= value;

                TryInvokeValueChangeEvent(Value, prevValue);
            }
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

        public StatSO Clone()
        {
            StatSO stat = Instantiate(this);
            return stat;
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall += RenameAsset;
        }

        private void RenameAsset()
        {
            if (this == null)
                return;
            
            string assetName = $"SO_{StatName}_Stat";
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(this), assetName);
        }
#endif
    }
}