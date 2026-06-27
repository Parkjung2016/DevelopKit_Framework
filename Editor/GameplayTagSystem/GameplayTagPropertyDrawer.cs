using System.Reflection;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary><see cref="GameplayTag"/> 필드용 인스펙터 드로어입니다.</summary>
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public sealed class GameplayTagPropertyDrawer : PropertyDrawer
    {
        private static readonly GUIContent TempContent = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            SerializedProperty nameProperty = property.FindPropertyRelative("serializedTagName");
            GameplayTag tag = GameplayTagManager.RequestTag(nameProperty.stringValue);

            if (!tag.IsValid && !string.IsNullOrEmpty(nameProperty.stringValue))
            {
                TempContent.text = string.Format(GameplayTagEditorLocalization.DrawerInvalidTag, nameProperty.stringValue);
                TempContent.tooltip = GameplayTagEditorLocalization.DrawerInvalidTagTooltip;
            }
            else if (!tag.IsNone)
            {
                TempContent.text = tag.Name;
                TempContent.tooltip = tag.Description;
            }
            else
            {
                TempContent.text = GameplayTagEditorLocalization.DrawerSelectTag;
                TempContent.tooltip = GameplayTagEditorLocalization.DrawerSelectTagTooltip;
            }

            if (EditorGUI.DropdownButton(position, TempContent, FocusType.Keyboard))
            {
                string parentFilter = GetParentFilter(fieldInfo);
                GameplayTagPickerWindow.ShowSingle(position, nameProperty, parentFilter, null);
            }

            EditorGUI.indentLevel = oldIndent;
            EditorGUI.EndProperty();
        }

        private static string GetParentFilter(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                return null;

            var filterAttrs = fieldInfo.GetCustomAttributes(typeof(ShowOnlyChildTagOfAttribute), true);
            if (filterAttrs.Length == 0)
                return null;

            return ((ShowOnlyChildTagOfAttribute)filterAttrs[0]).ParentTagName;
        }
    }
}
