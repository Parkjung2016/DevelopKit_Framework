using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [Serializable]
    public sealed class StatOverride
    {
        [SerializeField] private StatId id = default;
        [SerializeField] private string displayName = null;
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 100f;
        [SerializeField] private float defaultValue = 0f;
        [SerializeField] private Sprite icon = null;
        [SerializeField] private bool overrideBaseValue = false;

        [SerializeField, ShowIf("overrideBaseValue")]
        private float baseValue = 0f;

        public StatId Id => id;
        public bool IsValid => id.IsValid;

        public StatOverrideEntry CreateEntry()
        {
            if (!IsValid)
                return default;

            var definition = new StatDefinition(
                id,
                displayName,
                minValue,
                maxValue,
                defaultValue,
                icon);
            return new StatOverrideEntry(definition, overrideBaseValue, baseValue);
        }
    }
}