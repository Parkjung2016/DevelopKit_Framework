using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 런타임 스탯을 만드는 데 필요한 변경 불가능한 초기 데이터입니다.
    /// </summary>
    public readonly struct StatDefinition
    {
        public string StatName { get; }
        public string DisplayName { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
        public float BaseValue { get; }
        public Sprite StatIcon { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(StatName);

        public StatDefinition(
            string statName,
            string displayName = null,
            float minValue = 0f,
            float maxValue = 0f,
            float baseValue = 0f,
            Sprite statIcon = null)
        {
            if (maxValue < minValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "MaxValue must be greater than or equal to MinValue.");

            StatName = statName?.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? StatName : displayName.Trim();
            MinValue = minValue;
            MaxValue = maxValue;
            BaseValue = Math.Clamp(baseValue, minValue, maxValue);
            StatIcon = statIcon;
        }
    }
}