using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [Serializable]
    public sealed class StatOverride
    {
        [SerializeField] private StatSO stat = null;
        [SerializeField] private bool isUseOverride = false;
        [SerializeField] private float overrideValue = 0f;

        public StatSO Stat => stat;
        public bool OverrideBaseValue => isUseOverride;
        public float BaseValue => overrideValue;
        public bool IsValid => stat != null;

        public StatOverrideEntry CreateEntry() =>
            IsValid
                ? new StatOverrideEntry(stat.CreateDefinition(), isUseOverride, overrideValue)
                : default;
    }
}