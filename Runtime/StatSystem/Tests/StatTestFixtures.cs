using System.Collections.Generic;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    internal static class StatTestFixtures
    {
        public static StatCollection CreateCollectionFromSharedDatabase() =>
            CreateCollection(StatTestDatabase.Shared);

        public static StatCollection CreateCollection(
            IStatCatalog catalog,
            IReadOnlyList<StatOverrideEntry> overrides = null)
        {
            var collection = new StatCollection();
            collection.Initialize(catalog, overrides);
            return collection;
        }

        public static Stat CreateHpStat(float baseValue = 50f) =>
            new(new StatDefinition(StatTestDatabase.HpStatName, "Health", 0f, 100f, baseValue));
    }
}