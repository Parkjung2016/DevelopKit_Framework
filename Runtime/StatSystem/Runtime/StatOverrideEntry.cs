namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public readonly struct StatOverrideEntry
    {
        public StatDefinition Definition { get; }
        public bool UseOverride { get; }
        public float OverrideValue { get; }

        public string StatName => Definition.StatName;

        public StatOverrideEntry(in StatDefinition definition, bool useOverride = false, float overrideValue = 0f)
        {
            Definition = definition;
            UseOverride = useOverride;
            OverrideValue = overrideValue;
        }

        public Stat CreateStat()
        {
            Stat stat = Stat.CreateFrom(Definition);
            if (UseOverride)
                stat.BaseValue = OverrideValue;

            return stat;
        }
    }
}
