#if !UNITY_6000_5_OR_NEWER
using PJDev.DevelopKit.Framework.Shared.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class SnowflakeItemInstanceIdGenerator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterLegacyPlayModeCleanup()
        {
            FrameworkPlayModeCleanup.Register(ResetPlayModeStatics);
        }

        private static void ResetPlayModeStatics() => instance = new();
    }

    public sealed partial class DefaultItemInstanceIdGenerator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterLegacyPlayModeCleanup()
        {
            FrameworkPlayModeCleanup.Register(ResetPlayModeStatics);
        }

        private static void ResetPlayModeStatics() => instance = new();
    }
}
#endif
