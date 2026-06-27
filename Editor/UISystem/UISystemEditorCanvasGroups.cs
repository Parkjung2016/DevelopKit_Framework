using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal sealed class CanvasGroupOptionList
    {
        public List<string> Values { get; } = new();
        public List<string> Labels { get; } = new();
    }

    internal static class UISystemEditorCanvasGroups
    {
        public static event Action CanvasGroupsChanged;

        public static void NotifyCanvasGroupsChanged() => CanvasGroupsChanged?.Invoke();

        public static CanvasGroupOptionList BuildOptions(string currentGroupId, UILayerSettings settings = null)
        {
            var options = new CanvasGroupOptionList();
            settings ??= UISystemEditorAssets.LoadOrFindLayerSettings();
            settings?.EnsureDefaults();

            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (settings != null)
            {
                IReadOnlyList<UICanvasGroupDefinition> groups = settings.CanvasGroups;
                for (int i = 0; i < groups.Count; i++)
                {
                    UICanvasGroupDefinition group = groups[i];
                    if (group == null || string.IsNullOrEmpty(group.GroupId))
                        continue;

                    if (!seen.Add(group.GroupId))
                        continue;

                    options.Values.Add(group.GroupId);
                    options.Labels.Add(FormatGroupLabel(group));
                }
            }
            else
            {
                IReadOnlyList<string> builtIn = UISystemBuiltIn.CanvasGroupIds;
                for (int i = 0; i < builtIn.Count; i++)
                {
                    string groupId = builtIn[i];
                    if (!seen.Add(groupId))
                        continue;

                    options.Values.Add(groupId);
                    options.Labels.Add(FormatBuiltInLabel(groupId));
                }
            }

            if (!string.IsNullOrEmpty(currentGroupId) && !options.Values.Contains(currentGroupId))
            {
                options.Values.Add(currentGroupId);
                options.Labels.Add($"{currentGroupId} (현재 값)");
            }

            if (options.Values.Count == 0)
            {
                options.Values.Add(UICanvasGroups.Floating);
                options.Labels.Add(FormatBuiltInLabel(UICanvasGroups.Floating));
            }

            return options;
        }

        public static int GetSelectedIndex(CanvasGroupOptionList options, string groupId)
        {
            for (int i = 0; i < options.Values.Count; i++)
            {
                if (options.Values[i] == groupId)
                    return i;
            }

            return 0;
        }

        public static bool DrawCanvasGroupPopup(
            SerializedProperty groupIdProp,
            UILayerSettings settings,
            GUIContent label = null)
        {
            if (groupIdProp == null)
                return false;

            label ??= new GUIContent("Canvas 묶음");

            string currentGroupId = groupIdProp.stringValue;
            if (string.IsNullOrEmpty(currentGroupId))
                currentGroupId = ReadLegacyCanvasGroupId(groupIdProp);

            CanvasGroupOptionList options = BuildOptions(currentGroupId, settings);
            int selectedIndex = GetSelectedIndex(options, currentGroupId);

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, options.Labels.ToArray());
            if (!EditorGUI.EndChangeCheck() || newIndex < 0 || newIndex >= options.Values.Count)
                return false;

            string newGroupId = options.Values[newIndex];
            if (groupIdProp.stringValue == newGroupId)
                return false;

            Undo.RecordObject(groupIdProp.serializedObject.targetObject, "Change Canvas Group");
            groupIdProp.stringValue = newGroupId;
            groupIdProp.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(groupIdProp.serializedObject.targetObject);
            NotifyCanvasGroupsChanged();
            return true;
        }

        public static void DrawCanvasGroupPopup(
            Rect position,
            SerializedProperty groupIdProp,
            UILayerSettings settings,
            GUIContent label)
        {
            if (groupIdProp == null)
                return;

            label = EditorGUI.BeginProperty(position, label, groupIdProp);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            string currentGroupId = groupIdProp.stringValue;
            if (string.IsNullOrEmpty(currentGroupId))
                currentGroupId = ReadLegacyCanvasGroupId(groupIdProp);

            CanvasGroupOptionList options = BuildOptions(currentGroupId, settings);
            int selectedIndex = GetSelectedIndex(options, currentGroupId);

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, selectedIndex, options.Labels.ToArray());
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < options.Values.Count)
            {
                string newGroupId = options.Values[newIndex];
                if (groupIdProp.stringValue != newGroupId)
                {
                    Undo.RecordObject(groupIdProp.serializedObject.targetObject, "Change Canvas Group");
                    groupIdProp.stringValue = newGroupId;
                    groupIdProp.serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(groupIdProp.serializedObject.targetObject);
                    NotifyCanvasGroupsChanged();
                }
            }

            EditorGUI.EndProperty();
        }

        public static string FormatGroupLabel(string groupId, UILayerSettings settings = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return "-";

            settings ??= UISystemEditorAssets.LoadOrFindLayerSettings();
            settings?.EnsureDefaults();

            if (settings != null)
            {
                IReadOnlyList<UICanvasGroupDefinition> groups = settings.CanvasGroups;
                for (int i = 0; i < groups.Count; i++)
                {
                    UICanvasGroupDefinition group = groups[i];
                    if (group != null && group.GroupId == groupId)
                        return FormatGroupLabel(group);
                }
            }

            return FormatBuiltInLabel(groupId);
        }

        private static string FormatGroupLabel(UICanvasGroupDefinition group)
        {
            string displayName = group.DisplayName;
            return string.Equals(displayName, group.GroupId, StringComparison.Ordinal)
                ? group.GroupId
                : $"{group.GroupId} · {displayName}";
        }

        private static string FormatBuiltInLabel(string groupId)
        {
            if (UISystemBuiltIn.TryGetCanvasGroup(groupId, out BuiltInCanvasGroupInfo info))
                return $"{groupId} · {info.Description}";

            return groupId;
        }

        public static string ReadLegacyCanvasGroupId(SerializedProperty groupIdProp)
        {
            string path = groupIdProp.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0)
                return UICanvasGroups.Floating;

            SerializedProperty legacyProp = groupIdProp.serializedObject.FindProperty(
                path.Substring(0, lastDot + 1) + "canvasGroup");
            if (legacyProp != null && legacyProp.propertyType == SerializedPropertyType.Enum)
                return UICanvasGroupUtility.EnumToId((UICanvasGroup)legacyProp.enumValueIndex);

            return UICanvasGroups.Floating;
        }
    }
}
