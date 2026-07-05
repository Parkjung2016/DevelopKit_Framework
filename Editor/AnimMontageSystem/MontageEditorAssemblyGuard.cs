using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [InitializeOnLoad]
    internal static class MontageEditorAssemblyGuard
    {
        static MontageEditorAssemblyGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            MontageViewportInput.Shutdown();
            MontageSceneViewNavigation.Shutdown();

            Object preferredSelection = null;
            AnimMontageEditorWindow[] windows = Resources.FindObjectsOfTypeAll<AnimMontageEditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] == null)
                    continue;

                preferredSelection ??= windows[i].GetPreferredSelectionForReload();
                windows[i].HandleBeforeAssemblyReload();
            }

            MontageEditorSelectionUtility.SanitizeForDomainReload(preferredSelection);
        }

        private static void OnAfterAssemblyReload()
        {
            MontageEditorSelectionUtility.SanitizeForDomainReload();
        }
    }
}
