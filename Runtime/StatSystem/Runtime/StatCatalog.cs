using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.Shared.Runtime;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class StatCatalog
    {
        public static bool IsReady => GlobalRegistry<IStatCatalog>.IsReady;

        public static IStatCatalog Current =>
            GlobalRegistry<IStatCatalog>.ResolveOrDefault(null, NullStatCatalog.Instance);

        public static void Set(IStatCatalog catalog) => GlobalRegistry<IStatCatalog>.Set(catalog);

        public static void Clear() => GlobalRegistry<IStatCatalog>.Clear();

        public static IStatCatalog Resolve(IStatCatalog catalog = null) => catalog ?? Current;

        public static IStatDatabase ResolveDatabase(IStatDatabase database = null) =>
            database ?? Current;

        public static bool TryGetDefinition(string statName, out StatDefinition definition) =>
            Current.TryGetDefinition(statName, out definition);
    }

    internal sealed class NullStatCatalog : IStatCatalog
    {
        public static readonly NullStatCatalog Instance = new();

        public IReadOnlyCollection<string> StatNames => Array.Empty<string>();

        public bool TryGetDefinition(string statName, out StatDefinition definition)
        {
            definition = default;
            return false;
        }

        public bool TryGetEntry(string statName, out StatCatalogEntry entry)
        {
            entry = default;
            return false;
        }
    }
}
