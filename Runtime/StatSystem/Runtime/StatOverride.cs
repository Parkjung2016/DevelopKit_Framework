using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [Serializable]
    public sealed class StatOverride
    {
        [SerializeField] private StatSO stat = null;
        [SerializeField] private bool overrideBaseValue = false;

        [SerializeField, ShowIf("overrideBaseValue")]
        private float baseValue = 0f;

        public StatSO Stat => stat;
        public StatId Id => stat != null ? stat.Id : default;
        public bool IsValid => stat != null;

        public StatOverrideEntry CreateEntry()
        {
            if (!IsValid)
                return default;

            return new StatOverrideEntry(
                stat.CreateDefinition(),
                overrideBaseValue,
                baseValue);
        }
    }
}
