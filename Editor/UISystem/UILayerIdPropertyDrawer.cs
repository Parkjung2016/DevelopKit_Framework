using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    [CustomPropertyDrawer(typeof(UILayerIdAttribute))]
    public sealed class UILayerIdPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var view = property.serializedObject.targetObject as UIViewBase;
            UISystemEditorLayers.DrawLayerIdPopup(position, property, view, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;
    }
}
