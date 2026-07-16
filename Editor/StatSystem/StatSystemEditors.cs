using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.StatSystem
{
    [CustomEditor(typeof(StatSO))]
    internal sealed class StatSOEditor : Editor
    {
        private const string StatNameControl = "StatSO.StatName";

        private SerializedProperty statNameProperty;
        private string statNameDraft;

        private void OnEnable()
        {
            statNameProperty = serializedObject.FindProperty("statName");
            statNameDraft = statNameProperty?.stringValue ?? string.Empty;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool isEditingName = GUI.GetNameOfFocusedControl() == StatNameControl;
            if (!isEditingName)
                statNameDraft = statNameProperty.stringValue;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Script",
                    MonoScript.FromScriptableObject((StatSO)target),
                    typeof(MonoScript),
                    false);
            }

            Event currentEvent = Event.current;
            bool confirmName = isEditingName &&
                               currentEvent.type == EventType.KeyDown &&
                               (currentEvent.keyCode == KeyCode.Return ||
                                currentEvent.keyCode == KeyCode.KeypadEnter);

            GUI.SetNextControlName(StatNameControl);
            statNameDraft = EditorGUILayout.TextField("Stat Name", statNameDraft);
            DrawPropertiesExcluding(serializedObject, "m_Script", "statName");

            if (confirmName)
            {
                statNameProperty.stringValue = statNameDraft.Trim();
                if (Event.current.type != EventType.Used)
                    Event.current.Use();
                GUI.FocusControl(null);
            }

            serializedObject.ApplyModifiedProperties();

            var stat = (StatSO)target;
            if (confirmName)
            {
                string confirmedName = stat.StatName;
                EditorApplication.delayCall += () => RenameAsset(stat, confirmedName);
            }

            if (string.IsNullOrWhiteSpace(stat.StatName))
                EditorGUILayout.HelpBox("Stat Name is required.", MessageType.Warning);

            if (stat.MaxValue < stat.MinValue)
                EditorGUILayout.HelpBox("Max Value must be greater than or equal to Min Value.", MessageType.Error);
        }

        private static void RenameAsset(StatSO stat, string statName)
        {
            if (stat == null || string.IsNullOrWhiteSpace(statName))
                return;

            string path = AssetDatabase.GetAssetPath(stat);
            if (string.IsNullOrEmpty(path))
                return;

            string assetName = $"SO_{SanitizeFileName(statName.Trim())}_Stat";
            if (string.IsNullOrEmpty(assetName) ||
                string.Equals(Path.GetFileNameWithoutExtension(path), assetName, StringComparison.Ordinal))
            {
                return;
            }

            string error = AssetDatabase.RenameAsset(path, assetName);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"Stat asset rename failed: {error}", stat);
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
                value = value.Replace(invalidCharacters[i], '_');

            return value.Replace('/', '_').Replace('\\', '_');
        }
    }

    [CustomEditor(typeof(StatDatabaseSO))]
    internal sealed class StatDatabaseSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            var database = (StatDatabaseSO)target;
            if (changed)
                database.RebuildCache();

            DrawValidation(database);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Rebuild Cache", GUILayout.Height(24f)))
                database.RebuildCache();
        }

        private static void DrawValidation(StatDatabaseSO database)
        {
            StatSO[] stats = database.Stats;
            var names = new HashSet<string>(System.StringComparer.Ordinal);
            int missingCount = 0;
            int invalidNameCount = 0;
            int duplicateCount = 0;

            for (int i = 0; i < stats.Length; i++)
            {
                StatSO stat = stats[i];
                if (stat == null)
                {
                    missingCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(stat.StatName))
                {
                    invalidNameCount++;
                    continue;
                }

                if (!names.Add(stat.StatName))
                    duplicateCount++;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Catalog Summary",
                $"{database.Definitions.Count} valid / {stats.Length} entries");

            if (missingCount > 0)
                EditorGUILayout.HelpBox($"{missingCount} missing asset reference(s).", MessageType.Warning);
            if (invalidNameCount > 0)
                EditorGUILayout.HelpBox($"{invalidNameCount} stat(s) have no name.", MessageType.Warning);
            if (duplicateCount > 0)
                EditorGUILayout.HelpBox($"{duplicateCount} duplicate stat name(s). The first entry is used.", MessageType.Error);
        }
    }

    [CustomEditor(typeof(ObjectStatSystem))]
    internal sealed class ObjectStatSystemEditor : Editor
    {
        private bool showRuntimeStats = true;

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox("Runtime stat values are shown in Play Mode.", MessageType.Info);
                return;
            }

            DrawRuntimeStats((ObjectStatSystem)target);
        }

        private void DrawRuntimeStats(ObjectStatSystem system)
        {
            EditorGUILayout.Space(8f);
            showRuntimeStats = EditorGUILayout.Foldout(
                showRuntimeStats,
                $"Runtime Stats ({system.StatCollection.Count})",
                true);

            if (!showRuntimeStats)
                return;

            if (!system.IsInitialized)
            {
                EditorGUILayout.HelpBox("The stat system has not been initialized.", MessageType.Warning);
                return;
            }

            foreach (Stat stat in system.StatCollection)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        string.IsNullOrEmpty(stat.DisplayName) ? stat.StatName : stat.DisplayName,
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Name", stat.StatName);
                    EditorGUILayout.LabelField("Value", stat.Value.ToString("0.###"));
                    EditorGUILayout.LabelField("Base Value", stat.BaseValue.ToString("0.###"));
                    EditorGUILayout.LabelField("Modifiers", stat.ModifierCount.ToString());
                }
            }
        }
    }
}