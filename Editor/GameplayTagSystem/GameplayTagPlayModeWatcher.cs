using PJDev.DevelopKit.BasicTemplate.Runtime;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>플레이 모드 진입 시 태그 재로드 여부를 경고합니다.</summary>
    [InitializeOnLoad]
    internal static class GameplayTagPlayModeWatcher
    {
        static GameplayTagPlayModeWatcher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
                return;

            if (!GameplayTagManager.HasBeenReloaded)
                return;

            CDebug.LogWarning(GameplayTagEditorLocalization.PlayModeReloadWarning);
        }
    }
}
