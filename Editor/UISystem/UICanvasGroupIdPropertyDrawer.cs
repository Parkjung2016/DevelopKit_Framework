using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    [CustomPropertyDrawer(typeof(UICanvasGroupIdAttribute))]
    public sealed class UICanvasGroupIdPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            UILayerSettings settings = UISystemEditorAssets.LoadOrFindLayerSettings();
            UISystemEditorCanvasGroups.DrawCanvasGroupPopup(position, property, settings, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;
    }
}
