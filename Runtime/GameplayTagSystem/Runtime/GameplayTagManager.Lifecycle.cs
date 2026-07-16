#if !UNITY_6000_5_OR_NEWER
using PJDev.DevelopKit.Framework.Shared.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    public static partial class GameplayTagManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterLegacyPlayModeCleanup()
        {
            FrameworkPlayModeCleanup.Register(ResetPlayModeStatics);
        }

        private static void ResetPlayModeStatics()
        {
            isInitialized = false;
            hasBeenReloaded = false;
            currentGeneration = 0;
            indexRemapsByGeneration.Clear();
            tagDefinitionsByName?.Clear();
            tagDefinitions = null;
            tagLookUpTable = null;
            tags = null;
        }
    }
}
#endif
