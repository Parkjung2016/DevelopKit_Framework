using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEditorStyleSheet
    {
        private static StyleSheet cachedSheet;

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

            string path = AssetDatabase.GUIDToAssetPath(InventoryEditorStyles.StylesAssetGuid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError(
                    $"Inventory Data Editor: StyleSheet GUID '{InventoryEditorStyles.StylesAssetGuid}' could not be resolved.");
                return null;
            }

            cachedSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (cachedSheet == null)
            {
                Debug.LogError(
                    $"Inventory Data Editor: StyleSheet asset at '{path}' (GUID {InventoryEditorStyles.StylesAssetGuid}) could not be loaded.");
            }

            return cachedSheet;
        }
    }
}
