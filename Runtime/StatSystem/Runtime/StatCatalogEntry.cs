namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public readonly struct StatCatalogEntry
    {
        public StatDefinition Definition { get; }

        public string StatName => Definition.StatName;
        public string DisplayName => Definition.DisplayName;

        public StatCatalogEntry(in StatDefinition definition) => Definition = definition;

        public static StatCatalogEntry FromDefinition(in StatDefinition definition) => new(definition);
    }
}
