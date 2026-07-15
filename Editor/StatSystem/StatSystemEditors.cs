using System.Collections.Generic;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.StatSystem
{
    [CustomEditor(typeof(StatSO))]
    internal sealed class StatSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var stat = (StatSO)target;
            if (string.IsNullOrWhiteSpace(stat.StatName))
                EditorGUILayout.HelpBox("Stat Name is required.", MessageType.Warning);

            if (stat.MaxValue < stat.MinValue)
                EditorGUILayout.HelpBox("Max Value must be greater than or equal to Min Value.", MessageType.Error);
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
                $"Runtime Stats ({system.Stats.Count})",
                true);

            if (!showRuntimeStats)
                return;

            if (!system.IsInitialized)
            {
                EditorGUILayout.HelpBox("The stat system has not been initialized.", MessageType.Warning);
                return;
            }

            foreach (Stat stat in system.Stats)
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