#if !UNITY_6000_5_OR_NEWER
using PJDev.DevelopKit.Framework.Shared.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Core.Runtime
{
    public static partial class FrameworkGlobals
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterLegacyPlayModeCleanup()
        {
            FrameworkPlayModeCleanup.Register(ClearCatalogs);
        }
    }
}
#elif UNITY_6000_5_OR_NEWER
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Core.Runtime
{
    public static partial class FrameworkGlobals
    {
        [OnExitingPlayMode]
        private static void OnExitingPlayMode()
        {
            ClearCatalogs();
        }
    }
}
#endif
