using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>Gameplay Tag 에디터의 UIElements 스타일을 불러와 적용합니다.</summary>
    internal static class GameplayTagEditorStyleSheet
    {
        private static StyleSheet cachedSheet;

        /// <summary>루트 요소에 Gameplay Tag 에디터 스타일을 적용합니다.</summary>
        public static void Apply(VisualElement root)
        {
            StyleSheet sheet = GetOrLoad();
            if (sheet == null)
                return;

            if (!root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);
        }

        private static StyleSheet GetOrLoad()
        {
            if (cachedSheet != null)
                return cachedSheet;

            string path = AssetDatabase.GUIDToAssetPath(GameplayTagEditorStyles.StylesAssetGuid);
            if (string.IsNullOrEmpty(path))
            {
                CDebug.LogError(
                    $"Gameplay Tag Editor: StyleSheet GUID '{GameplayTagEditorStyles.StylesAssetGuid}' could not be resolved.");
                return null;
            }

            cachedSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (cachedSheet == null)
            {
                CDebug.LogError(
                    $"Gameplay Tag Editor: StyleSheet asset at '{path}' could not be loaded.");
            }

            return cachedSheet;
        }
    }
}
