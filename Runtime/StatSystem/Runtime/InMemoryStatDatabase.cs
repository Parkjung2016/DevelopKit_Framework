using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public class InMemoryStatDatabase : IStatCatalog
    {
        private readonly Dictionary<string, StatCatalogEntry> entries = new();

        public IReadOnlyCollection<string> StatNames => entries.Keys;

        public void Clear() => entries.Clear();

        public void Register(in StatDefinition definition) =>
            Register(StatCatalogEntry.FromDefinition(definition));

        public void Register(in StatCatalogEntry entry)
        {
            if (string.IsNullOrEmpty(entry.StatName))
                return;

            entries[entry.StatName] = entry;
        }

        public void RegisterRange(IEnumerable<StatDefinition> source)
        {
            if (source == null)
                return;

            foreach (StatDefinition definition in source)
                Register(definition);
        }

        public void RegisterRange(IEnumerable<StatCatalogEntry> source)
        {
            if (source == null)
                return;

            foreach (StatCatalogEntry entry in source)
                Register(entry);
        }

        public bool TryGetDefinition(string statName, out StatDefinition definition)
        {
            if (entries.TryGetValue(statName, out StatCatalogEntry entry))
            {
                definition = entry.Definition;
                return true;
            }

            definition = default;
            return false;
        }

        public bool TryGetEntry(string statName, out StatCatalogEntry entry) =>
            entries.TryGetValue(statName, out entry);
    }
}
