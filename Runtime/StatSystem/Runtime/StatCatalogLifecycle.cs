#if !UNITY_6000_5_OR_NEWER
using PJDev.DevelopKit.Framework.Shared.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    internal static class StatCatalogLifecycle
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterCleanup() =>
            FrameworkPlayModeCleanup.Register(StatCatalog.Clear);
    }
}
#endif