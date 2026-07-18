namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public readonly struct StatOverrideEntry
    {
        public StatDefinition Definition { get; }
        public bool OverrideBaseValue { get; }
        public float BaseValue { get; }

        public StatId Id => Definition.Id;
        public bool IsValid => Definition.IsValid;

        public StatOverrideEntry(
            in StatDefinition definition,
            bool overrideBaseValue = false,
            float baseValue = 0f)
        {
            Definition = definition;
            OverrideBaseValue = overrideBaseValue;
            BaseValue = baseValue;
        }

        public Stat CreateStat()
        {
            Stat stat = new(Definition);
            if (OverrideBaseValue)
                stat.BaseValue = BaseValue;

            return stat;
        }
    }
}
