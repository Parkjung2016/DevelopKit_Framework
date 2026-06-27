using System.Reflection;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary><see cref="GameplayTagContainer"/> 필드용 인스펙터 드로어입니다.</summary>
    [CustomPropertyDrawer(typeof(GameplayTagContainer))]
    public sealed class GameplayTagContainerPropertyDrawer : PropertyDrawer
    {
        private const float Gap = 4f;
        private const float ChipHeight = 20f;
        private const float ButtonWidth = 88f;
        private const float ButtonHeight = 22f;

        private static GUIStyle chipStyle;
        private static GUIStyle invalidChipStyle;
        private static GUIContent editContent;
        private static GUIContent removeContent;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float headerHeight = ButtonHeight;
            float chipsHeight = GetChipsHeight(property);
            return Mathf.Max(headerHeight, chipsHeight) + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureStyles();

            label = EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            SerializedProperty explicitTags = property.FindPropertyRelative("serializedExplicitTags");

            Rect buttonRect = new(position.x, position.y, ButtonWidth, ButtonHeight);
            if (GUI.Button(buttonRect, editContent))
            {
                string parentFilter = GetParentFilter(fieldInfo);
                GameplayTagPickerWindow.ShowMulti(buttonRect, explicitTags, parentFilter);
            }

            Rect chipsRect = new(
                position.x + ButtonWidth + Gap,
                position.y,
                position.width - ButtonWidth - Gap,
                GetChipsHeight(property));

            DrawChips(chipsRect, explicitTags, property);

            EditorGUI.indentLevel = oldIndent;
            EditorGUI.EndProperty();
        }

        private static void DrawChips(Rect rect, SerializedProperty explicitTags, SerializedProperty property)
        {
            if (explicitTags.hasMultipleDifferentValues)
            {
                EditorGUI.LabelField(rect, GameplayTagEditorLocalization.DrawerMixedValues);
                return;
            }

            if (explicitTags.arraySize == 0)
            {
                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                EditorGUI.LabelField(rect, GameplayTagEditorLocalization.DrawerNoTags);
                GUI.color = prev;
                return;
            }

            float x = rect.x;
            float y = rect.y;
            float maxX = rect.xMax;

            for (int i = 0; i < explicitTags.arraySize; i++)
            {
                SerializedProperty element = explicitTags.GetArrayElementAtIndex(i);
                GameplayTag tag = GameplayTagManager.RequestTag(element.stringValue, false);
                bool isValid = tag.IsValid;

                GUIStyle style = isValid ? chipStyle : invalidChipStyle;
                string text = isValid ? element.stringValue : $"{element.stringValue}{GameplayTagEditorLocalization.DrawerInvalidSuffix}";
                Vector2 size = style.CalcSize(new GUIContent(text));
                float chipWidth = Mathf.Min(size.x + 24f, maxX - x);

                if (x + chipWidth > maxX)
                {
                    x = rect.x;
                    y += ChipHeight + Gap;
                }

                Rect chipRect = new(x, y, chipWidth, ChipHeight);
                Rect removeRect = new(chipRect.xMax - 18f, chipRect.y + 2f, 16f, 16f);
                Rect labelRect = new(chipRect.x + 6f, chipRect.y, chipRect.width - 22f, chipRect.height);

                GUI.Box(chipRect, GUIContent.none, style);
                GUI.Label(labelRect, new GUIContent(text, tag.Description));

                if (GUI.Button(removeRect, removeContent, EditorStyles.label))
                {
                    explicitTags.DeleteArrayElementAtIndex(i);
                    property.serializedObject.ApplyModifiedProperties();
                    return;
                }

                x += chipWidth + Gap;
            }
        }

        private static float GetChipsHeight(SerializedProperty property)
        {
            SerializedProperty explicitTags = property.FindPropertyRelative("serializedExplicitTags");
            if (explicitTags.hasMultipleDifferentValues || explicitTags.arraySize == 0)
                return EditorGUIUtility.singleLineHeight;

            return ChipHeight;
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

        private static void EnsureStyles()
        {
            if (chipStyle != null)
                return;

            chipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                padding = new RectOffset(6, 6, 2, 2),
                normal = { textColor = new Color(0.85f, 0.92f, 1f) }
            };

            invalidChipStyle = new GUIStyle(chipStyle)
            {
                normal = { textColor = new Color(1f, 0.55f, 0.55f) }
            };

            editContent = new GUIContent(GameplayTagEditorLocalization.DrawerEditTags, GameplayTagEditorLocalization.DrawerEditTagsTooltip);
            removeContent = new GUIContent
            {
                image = EditorGUIUtility.IconContent("Toolbar Minus").image,
                tooltip = GameplayTagEditorLocalization.DrawerRemoveTagTooltip
            };
        }
    }
}
