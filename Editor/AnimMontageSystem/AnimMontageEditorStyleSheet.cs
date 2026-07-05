using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class AnimMontageEditorStyleSheet
    {
        private static StyleSheet cached;

        public static void Apply(VisualElement root)
        {
            StyleSheet sheet = Load();
            if (sheet != null && !root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);
        }

        private static StyleSheet Load()
        {
            if (cached != null)
                return cached;

            string path = AssetDatabase.GUIDToAssetPath(AnimMontageEditorStyles.StylesAssetGuid);
            cached = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            return cached;
        }
    }
}
