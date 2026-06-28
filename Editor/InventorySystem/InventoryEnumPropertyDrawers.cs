using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    [CustomPropertyDrawer(typeof(ItemType))]
    internal sealed class ItemTypePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.LabelField(position, label.text, "ItemType enum expected");
                EditorGUI.EndProperty();
                return;
            }

            string[] names = property.enumDisplayNames;
            string[] values = property.enumNames;
            var options = new GUIContent[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                if (!System.Enum.TryParse(values[i], out ItemType itemType))
                {
                    options[i] = new GUIContent(names[i]);
                    continue;
                }

                string display = InventoryEnumCatalog.GetItemTypeDisplayName(itemType);
                options[i] = new GUIContent(display, values[i]);
            }

            property.enumValueIndex = EditorGUI.Popup(
                position,
                label,
                property.enumValueIndex,
                options);

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(ContainerKind))]
    internal sealed class ContainerKindPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.LabelField(position, label.text, "ContainerKind enum expected");
                EditorGUI.EndProperty();
                return;
            }

            string[] names = property.enumNames;
            var options = new GUIContent[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                if (!System.Enum.TryParse(names[i], out ContainerKind kind))
                {
                    options[i] = new GUIContent(names[i]);
                    continue;
                }

                string display = InventoryEnumCatalog.GetContainerKindDisplayName(kind);
                options[i] = new GUIContent(display, names[i]);
            }

            property.enumValueIndex = EditorGUI.Popup(
                position,
                label,
                property.enumValueIndex,
                options);

            EditorGUI.EndProperty();
        }
    }
}
