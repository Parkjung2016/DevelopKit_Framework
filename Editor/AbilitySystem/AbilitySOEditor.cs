using System;
using System.Linq;
using System.Reflection;
using PJDev.DevelopKit.Framework.AbilitySystem.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AbilitySystem
{
    [CustomEditor(typeof(AbilitySO), true)]
    internal sealed class AbilitySOEditor : Editor
    {
        private SerializedProperty effects;
        private ReorderableList effectList;

        private void OnEnable()
        {
            effects = serializedObject.FindProperty("effects");
            effectList = new ReorderableList(serializedObject, effects, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Effects"),
                drawElementCallback = DrawEffect,
                elementHeightCallback = GetEffectHeight,
                onAddDropdownCallback = ShowEffectMenu
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                DrawPropertiesExcluding(serializedObject, "m_Script", "effects");
                EditorGUILayout.Space(5f);
                effectList.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEffect(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty item = effects.GetArrayElementAtIndex(index);
            string label = item.managedReferenceValue?.GetType().Name ?? "Missing Effect";
            const float dragHandleWidth = 16f;
            rect.xMin += dragHandleWidth;
            rect.y += 2f;
            EditorGUI.PropertyField(rect, item, new GUIContent(label), true);
        }

        private float GetEffectHeight(int index)
        {
            SerializedProperty item = effects.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(item, true) + 4f;
        }

        private void ShowEffectMenu(Rect buttonRect, ReorderableList list)
        {
            var menu = new GenericMenu();
            Type[] types = TypeCache.GetTypesDerivedFrom<AbilityEffect>()
                                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    !type.IsGenericType &&
                    type.IsSerializable &&
                    type.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        binder: null,
                        Type.EmptyTypes,
                        modifiers: null) != null)
                .OrderBy(type => type.Name)
                .ToArray();

            if (types.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Effect Types"));
            }
            else
            {
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => AddEffect(type));
                }
            }

            menu.DropDown(buttonRect);
        }

        private void AddEffect(Type type)
        {
            serializedObject.Update();
            int index = effects.arraySize;
            effects.InsertArrayElementAtIndex(index);
            effects.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type, nonPublic: true);
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(AbilityStatCost))]
    internal sealed class AbilityStatCostDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position.height = EditorGUIUtility.singleLineHeight;
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("statId"), true);
            AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("amount"));
            SerializedProperty percent = property.FindPropertyRelative("percent");
            AbilityDrawerUtility.DrawNext(ref position, percent);
            if (percent.floatValue > 0f)
                AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("percentBase"));
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight;
            height = AbilityDrawerUtility.AddHeight(height, property.FindPropertyRelative("statId"), true);
            height = AbilityDrawerUtility.AddHeight(height, property.FindPropertyRelative("amount"));
            SerializedProperty percent = property.FindPropertyRelative("percent");
            height = AbilityDrawerUtility.AddHeight(height, percent);
            if (percent.floatValue > 0f)
                height = AbilityDrawerUtility.AddHeight(height, property.FindPropertyRelative("percentBase"));
            return height;
        }
    }

    [CustomPropertyDrawer(typeof(StatAbilityEffect))]
    internal sealed class StatAbilityEffectDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position.height = EditorGUIUtility.singleLineHeight;

            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("target"));
            AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("statId"), true);
            SerializedProperty mode = property.FindPropertyRelative("mode");
            AbilityDrawerUtility.DrawNext(ref position, mode);

            if ((StatEffectMode)mode.enumValueIndex == StatEffectMode.Modifier)
            {
                AbilityDrawerUtility.DrawNext(
                    ref position,
                    property.FindPropertyRelative("amount"),
                    label: new GUIContent("Flat Amount"));
                AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("percent"));
            }
            else
            {
                SerializedProperty operation = property.FindPropertyRelative("baseValueChange");
                AbilityDrawerUtility.DrawNext(ref position, operation);
                if ((BaseValueChange)operation.enumValueIndex == BaseValueChange.AddPercent)
                    AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("percent"));
                else
                    AbilityDrawerUtility.DrawNext(ref position, property.FindPropertyRelative("amount"));
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight;
            height = AbilityDrawerUtility.AddHeight(height, property.FindPropertyRelative("target"));
            height = AbilityDrawerUtility.AddHeight(height, property.FindPropertyRelative("statId"), true);
            SerializedProperty mode = property.FindPropertyRelative("mode");
            height = AbilityDrawerUtility.AddHeight(height, mode);

            if ((StatEffectMode)mode.enumValueIndex == StatEffectMode.Modifier)
            {
                height = AbilityDrawerUtility.AddHeight(height, property.FindPropertyRelative("amount"));
                height = AbilityDrawerUtility.AddHeight(height, property.FindPropertyRelative("percent"));
            }
            else
            {
                SerializedProperty operation = property.FindPropertyRelative("baseValueChange");
                height = AbilityDrawerUtility.AddHeight(height, operation);
                SerializedProperty value = (BaseValueChange)operation.enumValueIndex == BaseValueChange.AddPercent
                    ? property.FindPropertyRelative("percent")
                    : property.FindPropertyRelative("amount");
                height = AbilityDrawerUtility.AddHeight(height, value);
            }

            return height;
        }
    }

    internal static class AbilityDrawerUtility
    {
        public static void DrawNext(
            ref Rect position,
            SerializedProperty property,
            bool includeChildren = false,
            GUIContent label = null)
        {
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            position.height = EditorGUI.GetPropertyHeight(property, includeChildren);
            if (label == null)
                EditorGUI.PropertyField(position, property, includeChildren);
            else
                EditorGUI.PropertyField(position, property, label, includeChildren);
        }

        public static float AddHeight(
            float current,
            SerializedProperty property,
            bool includeChildren = false) =>
            current +
            EditorGUIUtility.standardVerticalSpacing +
            EditorGUI.GetPropertyHeight(property, includeChildren);
    }
}