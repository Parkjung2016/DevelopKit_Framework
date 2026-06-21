using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEditorStyleSheet
    {
        public static void Apply(VisualElement root)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(InventoryEditorStyles.StylesAssetPath);
            if (sheet == null)
            {
                Debug.LogError($"Inventory Data Editor: StyleSheet not found at '{InventoryEditorStyles.StylesAssetPath}'.");
                return;
            }

            if (!root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);
        }
    }
}
