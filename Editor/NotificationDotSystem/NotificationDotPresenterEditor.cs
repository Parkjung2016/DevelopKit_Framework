using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime;
using PJDev.DevelopKit.Framework.NotificationDotSystem.UI;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Editor
{
    [CustomEditor(typeof(NotificationDotPresenter))]
    public sealed class NotificationDotPresenterEditor : UnityEditor.Editor
    {
        private const float RowGap = 3f;

        private sealed class DotOption
        {
            public Type EnumType;
            public object Value;
            public string Key;
            public string EnumName;
            public string ValueName;
            public string DisplayName;
            public string Relation;
            public string TypeName;
            public long RawValue;
            public string Identity;
        }

        private readonly List<DotOption> options = new();
        private readonly Dictionary<string, DotOption> optionsByKey =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, DotOption> optionsByIdentity =
            new(StringComparer.Ordinal);

        private ReorderableList targetList;
        private SerializedProperty spawnPoint;
        private SerializedProperty targets;

        private void OnEnable()
        {
            spawnPoint = serializedObject.FindProperty("spawnPoint");
            targets = serializedObject.FindProperty("targets");
            BuildOptions();

            targetList = new ReorderableList(serializedObject, targets, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "감시할 알림"),
                drawElementCallback = DrawTarget,
                elementHeightCallback = GetTargetHeight,
                onAddDropdownCallback = (rect, _) => ShowAddPopup(rect)
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("표시", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(spawnPoint, new GUIContent("Spawn Point"));

            EditorGUILayout.Space(7);
            targetList.DoLayoutList();
            EditorGUILayout.HelpBox(
                "가장 높은 Priority 하나만 표시합니다. Priority가 같으면 목록 위쪽 항목이 우선합니다.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
                DrawRuntimeState((NotificationDotPresenter)target);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Runtime Monitor 열기"))
                NotificationDotMonitorWindow.Open();
        }

        private static void DrawRuntimeState(NotificationDotPresenter presenter)
        {
            EditorGUILayout.Space(6);
            string name = string.IsNullOrWhiteSpace(presenter.CurrentDisplayName)
                ? "없음"
                : presenter.CurrentDisplayName;
            EditorGUILayout.HelpBox(
                $"현재 표시: {name}\n개수: {presenter.CurrentCount}",
                MessageType.Info);
        }

        private float GetTargetHeight(int index)
        {
            if (index < 0 || index >= targets.arraySize)
                return EditorGUIUtility.singleLineHeight + 6f;

            SerializedProperty element = targets.GetArrayElementAtIndex(index);
            DotOption option = FindOption(element);
            int rows = option != null && !string.IsNullOrWhiteSpace(option.Relation) ? 3 : 2;
            return EditorGUIUtility.singleLineHeight * rows + RowGap * (rows - 1) + 6f;
        }

        private void DrawTarget(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = targets.GetArrayElementAtIndex(index);
            SerializedProperty priority = element.FindPropertyRelative("priority");
            DotOption option = FindOption(element);

            float line = EditorGUIUtility.singleLineHeight;
            float y = rect.y + 3f;
            EditorGUI.LabelField(
                new Rect(rect.x, y, rect.width, line),
                option?.DisplayName ?? "Missing Notification",
                EditorStyles.boldLabel);
            y += line + RowGap;

            if (!string.IsNullOrWhiteSpace(option?.Relation))
            {
                EditorGUI.LabelField(
                    new Rect(rect.x, y, rect.width, line),
                    option.Relation,
                    EditorStyles.miniLabel);
                y += line + RowGap;
            }

            DrawPriorityField(rect.x, y, rect.width, priority);
        }

        private DotOption FindOption(SerializedProperty element)
        {
            string typeName = element.FindPropertyRelative("enumTypeName").stringValue;
            long rawValue = element.FindPropertyRelative("enumValue").longValue;
            optionsByIdentity.TryGetValue(BuildIdentity(typeName, rawValue), out DotOption option);
            return option;
        }
        private static void DrawPriorityField(
            float x,
            float y,
            float width,
            SerializedProperty priority)
        {
            const float labelWidth = 58f;
            const float valueWidth = 72f;
            float line = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(x, y, labelWidth, line), "Priority", EditorStyles.miniLabel);
            priority.intValue = EditorGUI.IntField(
                new Rect(x + labelWidth, y, Mathf.Min(valueWidth, Mathf.Max(30f, width - labelWidth)), line),
                priority.intValue);
        }

        private void ShowAddPopup(Rect buttonRect)
        {
            BuildOptions();
            PopupWindow.Show(
                buttonRect,
                new DotSelectorPopup(options, AddEnumTarget));
        }

        private sealed class DotSelectorPopup : PopupWindowContent
        {
            private readonly IReadOnlyList<DotOption> options;
            private readonly Action<DotOption> onSelect;
            private readonly SearchField searchField = new();
            private Vector2 scroll;
            private string search = string.Empty;

            public DotSelectorPopup(
                IReadOnlyList<DotOption> options,
                Action<DotOption> onSelect)
            {
                this.options = options;
                this.onSelect = onSelect;
            }

            public override Vector2 GetWindowSize() => new(420f, 440f);

            public override void OnOpen()
            {
                searchField.SetFocus();
            }

            public override void OnGUI(Rect rect)
            {
                const float padding = 8f;
                const float searchHeight = 20f;

                Rect searchRect = new(padding, padding, rect.width - padding * 2f, searchHeight);
                search = searchField.OnGUI(searchRect, search);

                Rect listRect = new(
                    0f,
                    searchRect.yMax + 6f,
                    rect.width,
                    rect.height - searchRect.yMax - 12f);
                DrawOptions(listRect);
            }
            private void DrawOptions(Rect rect)
            {
                float contentHeight = CalculateContentHeight();
                Rect content = new(0f, 0f, rect.width - 14f, contentHeight);
                scroll = GUI.BeginScrollView(rect, scroll, content);

                float y = 0f;
                string currentEnum = null;
                bool found = false;
                for (int i = 0; i < options.Count; i++)
                {
                    DotOption option = options[i];
                    if (!MatchesSearch(option))
                        continue;

                    found = true;
                    if (!string.Equals(currentEnum, option.EnumName, StringComparison.Ordinal))
                    {
                        currentEnum = option.EnumName;
                        DrawHeader(new Rect(0f, y, content.width, 24f), currentEnum);
                        y += 24f;
                    }

                    float rowHeight = string.IsNullOrWhiteSpace(option.Relation) ? 26f : 42f;
                    DrawOption(new Rect(0f, y, content.width, rowHeight), option);
                    y += rowHeight;
                }

                if (!found)
                {
                    GUI.Label(
                        new Rect(12f, 12f, content.width - 24f, 24f),
                        "검색 결과가 없습니다.",
                        EditorStyles.centeredGreyMiniLabel);
                }

                GUI.EndScrollView();
            }

            private float CalculateContentHeight()
            {
                float height = 4f;
                string currentEnum = null;
                for (int i = 0; i < options.Count; i++)
                {
                    DotOption option = options[i];
                    if (!MatchesSearch(option))
                        continue;
                    if (!string.Equals(currentEnum, option.EnumName, StringComparison.Ordinal))
                    {
                        currentEnum = option.EnumName;
                        height += 24f;
                    }

                    height += string.IsNullOrWhiteSpace(option.Relation) ? 26f : 42f;
                }

                return Mathf.Max(height, 32f);
            }

            private bool MatchesSearch(DotOption option)
            {
                if (string.IsNullOrWhiteSpace(search))
                    return true;

                return option.EnumName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || option.ValueName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || (!string.IsNullOrWhiteSpace(option.Relation)
                        && option.Relation.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            private static void DrawHeader(Rect rect, string enumName)
            {
                Color color = EditorGUIUtility.isProSkin
                    ? new Color(0.18f, 0.18f, 0.18f, 1f)
                    : new Color(0.82f, 0.82f, 0.82f, 1f);
                EditorGUI.DrawRect(rect, color);
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y, rect.width - 20f, rect.height),
                    enumName,
                    EditorStyles.boldLabel);
            }

            private void DrawOption(Rect rect, DotOption option)
            {
                bool hovered = rect.Contains(Event.current.mousePosition);
                if (hovered)
                {
                    Color hover = EditorGUIUtility.isProSkin
                        ? new Color(0.25f, 0.36f, 0.49f, 0.7f)
                        : new Color(0.55f, 0.72f, 0.9f, 0.55f);
                    EditorGUI.DrawRect(rect, hover);
                }

                const float indent = 12f;
                GUI.Label(
                    new Rect(indent, rect.y + 3f, rect.width - indent - 8f, 19f),
                    option.ValueName,
                    EditorStyles.label);

                if (!string.IsNullOrWhiteSpace(option.Relation))
                {
                    GUI.Label(
                        new Rect(indent, rect.y + 21f, rect.width - indent - 8f, 17f),
                        option.Relation,
                        EditorStyles.miniLabel);
                }

                if (!GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    return;

                onSelect(option);
                editorWindow.Close();
            }
        }
        private void BuildOptions()
        {
            options.Clear();
            optionsByKey.Clear();
            optionsByIdentity.Clear();

            var enumTypes = new List<Type>(TypeCache.GetTypesWithAttribute<NotificationDotAttribute>());
            enumTypes.RemoveAll(type => !type.IsEnum || IsTestAssembly(type.Assembly));
            enumTypes.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));

            for (int typeIndex = 0; typeIndex < enumTypes.Count; typeIndex++)
            {
                Type enumType = enumTypes[typeIndex];
                Array values = Enum.GetValues(enumType);
                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    object value = values.GetValue(valueIndex);
                    string key = NotificationDotEnum.GetKey(enumType, value);
                    var option = new DotOption
                    {
                        EnumType = enumType,
                        Value = value,
                        Key = key,
                        EnumName = enumType.Name,
                        ValueName = value.ToString(),
                        DisplayName = $"{enumType.Name}.{value}",
                        TypeName = enumType.AssemblyQualifiedName,
                        RawValue = GetRawValue(enumType, value)
                    };

                    option.Identity = BuildIdentity(option.TypeName, option.RawValue);
                    options.Add(option);
                    optionsByKey[key] = option;
                    optionsByIdentity[option.Identity] = option;
                }
            }

            for (int i = 0; i < options.Count; i++)
                CompleteOption(options[i]);

            options.Sort((left, right) =>
            {
                int typeOrder = string.Compare(
                    left.EnumType.FullName,
                    right.EnumType.FullName,
                    StringComparison.Ordinal);
                return typeOrder != 0
                    ? typeOrder
                    : string.Compare(left.ValueName, right.ValueName, StringComparison.Ordinal);
            });
        }

        private static bool IsTestAssembly(System.Reflection.Assembly assembly)
        {
            string name = assembly.GetName().Name;
            return name.EndsWith(".Tests", StringComparison.Ordinal)
                || name.EndsWith("Tests", StringComparison.Ordinal);
        }

        private static string GetParentKey(string key)
        {
            int separator = key.LastIndexOf('/');
            return separator > 0 ? key.Substring(0, separator) : string.Empty;
        }
        private void CompleteOption(DotOption option)
        {
            string parentKey = GetParentKey(option.Key);
            optionsByKey.TryGetValue(parentKey, out DotOption parent);

            option.DisplayName = $"{option.EnumName}.{option.ValueName}";

            var relations = new List<string>(2);
            if (parent != null)
                relations.Add($"상위: {parent.EnumName}.{parent.ValueName}");

            NotificationDotDefinition definition =
                NotificationDotEnum.GetDefinition(option.EnumType, option.Value);
            if (definition.Dependencies.Count > 0)
            {
                var dependencyNames = new string[definition.Dependencies.Count];
                for (int i = 0; i < definition.Dependencies.Count; i++)
                {
                    string sourceKey = definition.Dependencies[i].SourceKey;
                    dependencyNames[i] = optionsByKey.TryGetValue(sourceKey, out DotOption source)
                        ? $"{source.EnumName}.{source.ValueName}"
                        : GetShortKey(sourceKey);
                }

                relations.Add($"종속: {string.Join(", ", dependencyNames)}");
            }

            option.Relation = string.Join(" | ", relations);
        }

        private static string GetShortKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "없음";

            int separator = key.LastIndexOf('/');
            return separator >= 0 && separator < key.Length - 1
                ? key.Substring(separator + 1)
                : key;
        }

        private void AddEnumTarget(DotOption option)
        {
            serializedObject.Update();

            for (int i = 0; i < targets.arraySize; i++)
            {
                SerializedProperty existing = targets.GetArrayElementAtIndex(i);
                string typeName = existing.FindPropertyRelative("enumTypeName").stringValue;
                long rawValue = existing.FindPropertyRelative("enumValue").longValue;
                if (BuildIdentity(typeName, rawValue) != option.Identity)
                    continue;

                targetList.index = i;
                serializedObject.ApplyModifiedProperties();
                Repaint();
                return;
            }

            int index = targets.arraySize;
            targets.InsertArrayElementAtIndex(index);
            SerializedProperty element = targets.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("enumTypeName").stringValue = option.TypeName;
            element.FindPropertyRelative("enumValue").longValue = option.RawValue;
            element.FindPropertyRelative("priority").intValue = 0;
            targetList.index = index;
            serializedObject.ApplyModifiedProperties();
        }

        private static string BuildIdentity(string typeName, long rawValue) =>
            string.Concat(typeName, "|", rawValue);

        private static long GetRawValue(Type enumType, object value)
        {
            Type underlying = Enum.GetUnderlyingType(enumType);
            bool unsigned = underlying == typeof(byte)
                || underlying == typeof(ushort)
                || underlying == typeof(uint)
                || underlying == typeof(ulong);
            return unsigned
                ? unchecked((long)Convert.ToUInt64(value))
                : Convert.ToInt64(value);
        }
    }
}