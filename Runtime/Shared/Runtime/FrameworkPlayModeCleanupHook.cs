#if !UNITY_6000_5_OR_NEWER
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PJDev.DevelopKit.Framework.Shared.Runtime
{
    internal static class FrameworkPlayModeCleanupHook
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
                FrameworkPlayModeCleanup.RunAll();
        }
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterPlayerHook()
        {
            Application.quitting += FrameworkPlayModeCleanup.RunAll;
        }
#endif
    }
}
#endif
