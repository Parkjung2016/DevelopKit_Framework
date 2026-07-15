using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 하나의 출처가 스탯에 적용하는 고정값과 비율값입니다.
    /// </summary>
    public readonly struct StatModifier
    {
        public float Flat { get; }
        public float Percent { get; }

        public bool IsEmpty =>
            Mathf.Approximately(Flat, 0f) &&
            Mathf.Approximately(Percent, 0f);

        public StatModifier(float flat = 0f, float percent = 0f)
        {
            Flat = flat;
            Percent = percent;
        }

        public StatModifier WithFlat(float flat) => new(flat, Percent);

        public StatModifier WithPercent(float percent) => new(Flat, percent);
    }
}