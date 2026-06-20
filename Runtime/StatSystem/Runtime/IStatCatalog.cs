using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public interface IStatCatalog : IStatDatabase
    {
        IReadOnlyCollection<string> StatNames { get; }
        bool TryGetEntry(string statName, out StatCatalogEntry entry);
    }
}
