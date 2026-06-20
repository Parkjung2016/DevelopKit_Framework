namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public readonly struct StatDefinition
    {
        public string StatName { get; }
        public string DisplayName { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
        public float BaseValue { get; }

        public StatDefinition(
            string statName,
            string displayName = null,
            float minValue = 0f,
            float maxValue = 0f,
            float baseValue = 0f)
        {
            StatName = statName;
            DisplayName = displayName;
            MinValue = minValue;
            MaxValue = maxValue;
            BaseValue = baseValue;
        }
    }
}
