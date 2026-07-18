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
            GlobalRegistry<IStatCatalog>.ResolveOrDefault(null, EmptyStatCatalog.Instance);

        public static void Set(IStatCatalog catalog) => GlobalRegistry<IStatCatalog>.Set(catalog);

        public static void Clear() => GlobalRegistry<IStatCatalog>.Clear();

        public static IStatCatalog Resolve(IStatCatalog catalog = null) => catalog ?? Current;

        public static bool TryGetDefinition(StatId id, out StatDefinition definition) =>
            Current.TryGetDefinition(id, out definition);
    }

    internal sealed class EmptyStatCatalog : IStatCatalog
    {
        public static readonly EmptyStatCatalog Instance = new();

        public IReadOnlyList<StatDefinition> Definitions => Array.Empty<StatDefinition>();

        public bool TryGetDefinition(StatId id, out StatDefinition definition)
        {
            definition = default;
            return false;
        }
    }
}