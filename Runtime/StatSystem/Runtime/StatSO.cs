using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_Stat", menuName = "PJDev/Stat System/Stat")]
    public sealed class StatSO : ScriptableObject
    {
        [SerializeField, Delayed]
        private string statName = null;

        [SerializeField]
        private string displayName = null;

        [SerializeField]
        private float minValue = 0f;

        [SerializeField]
        private float maxValue = 100f;

        [SerializeField]
        private float baseValue = 0f;

        [SerializeField]
        private Sprite statIcon = null;

        public StatId Id => new(statName);
        public string StatName => statName;
        public string DisplayName => displayName;
        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public float BaseValue => baseValue;
        public Sprite StatIcon => statIcon;

        public StatDefinition CreateDefinition() =>
            new(Id, displayName, minValue, maxValue, baseValue, statIcon);

        public Stat CreateStat() => new(CreateDefinition());

#if UNITY_EDITOR
        private void OnValidate()
        {
            statName = statName?.Trim();

            if (maxValue < minValue)
                maxValue = minValue;

            baseValue = Mathf.Clamp(baseValue, minValue, maxValue);
        }
#endif
    }
}