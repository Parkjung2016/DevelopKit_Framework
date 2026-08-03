using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Editor
{
    public sealed class NotificationDotMonitorWindow : EditorWindow
    {
        private readonly List<NotificationDotSnapshot> snapshots = new();
        private readonly List<NotificationDotSnapshot> clearBuffer = new();
        private Vector2 scroll;
        private string search = string.Empty;
        private string selectedKey;
        private bool includeInactive;
        private int changeAmount = 1;
        private int setCount;
        private double nextRefreshTime;

        [MenuItem("PJDev/Notification Dots/Runtime Monitor", false, 2310)]
        public static void Open()
        {
            var window = GetWindow<NotificationDotMonitorWindow>("Notification Dots");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextRefreshTime)
                return;

            nextRefreshTime = EditorApplication.timeSinceStartup + 0.15d;
            Refresh();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect and edit notification state.", MessageType.Info);
                return;
            }

            DrawSummary();
            DrawSelectedTools();
            DrawHeader();
            DrawRows();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(
                    search,
                    GUI.skin.FindStyle("ToolbarSearchTextField"),
                    GUILayout.MinWidth(80f),
                    GUILayout.ExpandWidth(true));
                includeInactive = GUILayout.Toggle(
                    includeInactive,
                    new GUIContent("Inactive", "Show notifications with a total count of 0."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(66f));

                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(
                            new GUIContent("Clear All", "Clear current values without breaking notification handles."),
                            EditorStyles.toolbarButton,
                            GUILayout.Width(58f)))
                    {
                        HideAll();
                    }
                }
            }
        }

        private void DrawSummary()
        {
            int active = 0;
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].IsActive)
                    active++;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"Active {active}  |  Keys {NotificationDots.Current.RegisteredKeyCount}  |  Definitions {NotificationDots.Current.RegisteredDefinitionCount}",
                EditorStyles.miniLabel);
        }

        private void DrawSelectedTools()
        {
            if (string.IsNullOrWhiteSpace(selectedKey))
            {
                EditorGUILayout.HelpBox("Select a notification to edit its runtime state.", MessageType.None);
                return;
            }

            int direct = NotificationDots.GetDirectCount(selectedKey);
            int total = NotificationDots.GetCount(selectedKey);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(GetLeafName(selectedKey), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        NotificationDots.HasCountOverride(selectedKey)
                            ? $"Direct {direct}  |  Total {total}  |  Override"
                            : $"Direct {direct}  |  Total {total}",
                        EditorStyles.miniLabel,
                        GUILayout.ExpandWidth(false));
                    if (GUILayout.Button(
                            new GUIContent("Copy", "Copy the full notification key."),
                            EditorStyles.miniButton,
                            GUILayout.Width(40f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = selectedKey;
                    }
                }

                EditorGUILayout.LabelField(selectedKey, EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Amount", GUILayout.Width(45f));
                    changeAmount = Mathf.Max(1, EditorGUILayout.IntField(changeAmount, GUILayout.Width(48f)));
                    if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(30f)))
                        ChangeSelected(-changeAmount);
                    if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(30f)))
                        ChangeSelected(changeAmount);

                    GUILayout.Space(8f);
                    EditorGUILayout.LabelField("Set", GUILayout.Width(24f));
                    setCount = Mathf.Max(0, EditorGUILayout.IntField(setCount, GUILayout.Width(56f)));
                    if (GUILayout.Button("Apply", EditorStyles.miniButton, GUILayout.Width(42f)))
                        SetSelected(setCount);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            new GUIContent("Clear Value", "Set the displayed count to 0."),
                            EditorStyles.miniButton))
                    {
                        SetSelected(0);
                    }

                    using (new EditorGUI.DisabledScope(!NotificationDots.HasCountOverride(selectedKey)))
                    {
                        if (GUILayout.Button(
                                new GUIContent("Use Live", "Remove the override and use the original runtime value."),
                                EditorStyles.miniButton))
                        {
                            RestoreLiveValue(selectedKey);
                        }
                    }

                    if (GUILayout.Button(
                            new GUIContent("Visit", "Simulate visiting this notification branch."),
                            EditorStyles.miniButton))
                    {
                        VisitBranch(selectedKey);
                    }

                }
            }
        }

        private static void DrawHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.DrawRect(
                rect,
                EditorGUIUtility.isProSkin
                    ? new Color(0.16f, 0.16f, 0.16f)
                    : new Color(0.78f, 0.78f, 0.78f));
            GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 118f, rect.height), "Notification", EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.xMax - 108f, rect.y, 50f, rect.height), "Direct", EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.xMax - 54f, rect.y, 48f, rect.height), "Total", EditorStyles.boldLabel);
        }

        private void DrawRows()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < snapshots.Count; i++)
            {
                NotificationDotSnapshot snapshot = snapshots[i];
                if (!MatchesSearch(snapshot.Key))
                    continue;

                Rect rect = EditorGUILayout.GetControlRect(false, 22f);
                bool active = snapshot.IsActive;
                bool selected = string.Equals(selectedKey, snapshot.Key, StringComparison.Ordinal);
                Color rowColor;
                if (selected)
                {
                    rowColor = EditorGUIUtility.isProSkin
                        ? new Color(0.2f, 0.36f, 0.53f, 0.72f)
                        : new Color(0.42f, 0.65f, 0.88f, 0.62f);
                }
                else if (active)
                {
                    rowColor = EditorGUIUtility.isProSkin
                        ? new Color(0.11f, 0.25f, 0.16f, 0.5f)
                        : new Color(0.72f, 0.9f, 0.76f, 0.58f);
                }
                else
                {
                    rowColor = EditorGUIUtility.isProSkin
                        ? new Color(0.12f, 0.12f, 0.12f, 0.55f)
                        : new Color(0.82f, 0.82f, 0.82f, 0.55f);
                }

                EditorGUI.DrawRect(rect, rowColor);
                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.y, 3f, rect.height),
                    active
                        ? new Color(0.26f, 0.78f, 0.4f, 1f)
                        : new Color(0.45f, 0.45f, 0.45f, 1f));

                int depth = GetDepth(snapshot.Key);
                Rect keyRect = new(
                    rect.x + 6f + depth * 12f,
                    rect.y + 1f,
                    rect.width - 118f - depth * 12f,
                    20f);
                Color previousColor = GUI.color;
                if (!active)
                    GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, 0.55f);

                GUI.Label(
                    keyRect,
                    new GUIContent(GetLeafName(snapshot.Key), GetDefinitionTooltip(snapshot.Key)));
                GUI.Label(new Rect(rect.xMax - 108f, rect.y + 1f, 50f, 20f), snapshot.DirectCount.ToString());
                GUI.Label(new Rect(rect.xMax - 54f, rect.y + 1f, 48f, 20f), snapshot.Count.ToString());
                GUI.color = previousColor;
                HandleRowInput(rect, snapshot);
            }

            EditorGUILayout.EndScrollView();
        }

        private void HandleRowInput(Rect rect, NotificationDotSnapshot snapshot)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition) || evt.type != EventType.MouseDown)
                return;

            selectedKey = snapshot.Key;
            setCount = snapshot.DirectCount;
            if (evt.button == 1)
                ShowContextMenu(snapshot.Key);

            evt.Use();
            Repaint();
        }

        private void ShowContextMenu(string key)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add 1"), false, () => Change(key, 1));
            menu.AddItem(new GUIContent("Remove 1"), false, () => Change(key, -1));
            menu.AddItem(new GUIContent("Clear Value"), false, () => Set(key, 0));

            if (NotificationDots.HasCountOverride(key))
                menu.AddItem(new GUIContent("Use Live Value"), false, () => RestoreLiveValue(key));
            else
                menu.AddDisabledItem(new GUIContent("Use Live Value"));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Visit"), false, () => VisitBranch(key));
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Copy Key"),
                false,
                () => EditorGUIUtility.systemCopyBuffer = key);
            menu.ShowAsContext();
        }
        private void HideAll()
        {
            NotificationDots.GetSnapshot(clearBuffer, includeInactive: false);
            using (NotificationDots.BeginBatch())
            {
                for (int i = 0; i < clearBuffer.Count; i++)
                {
                    string key = clearBuffer[i].Key;
                    NotificationDots.Clear(key);
                    NotificationDots.SetCountOverride(key, 0);
                }
            }

            Refresh();
            Repaint();
        }

        private void ChangeSelected(int amount) => Change(selectedKey, amount);

        private void Change(string key, int amount)
        {
            int current = NotificationDots.GetDirectCount(key);
            long next = (long)current + amount;
            NotificationDots.SetCountOverride(key, next <= 0 ? 0 : next >= int.MaxValue ? int.MaxValue : (int)next);
            Refresh();
            Repaint();
        }

        private void SetSelected(int count) => Set(selectedKey, count);

        private void Set(string key, int count)
        {
            NotificationDots.SetCountOverride(key, count);
            Refresh();
            Repaint();
        }

        private void RestoreLiveValue(string key)
        {
            NotificationDots.ClearCountOverride(key);
            Refresh();
            Repaint();
        }

        private void VisitBranch(string branchKey)
        {
            NotificationDots.GetSnapshot(clearBuffer, includeInactive: false);
            using (NotificationDots.BeginBatch())
            {
                for (int i = 0; i < clearBuffer.Count; i++)
                {
                    NotificationDotSnapshot snapshot = clearBuffer[i];
                    if (snapshot.DirectCount <= 0 || !IsInBranch(snapshot.Key, branchKey))
                        continue;

                    if (NotificationDots.TryGetDefinition(snapshot.Key, out NotificationDotDefinition definition)
                        && definition.ClearsOnVisit)
                    {
                        NotificationDots.Visit(snapshot.Key);
                    }
                    else
                    {
                        NotificationDots.SetCountOverride(snapshot.Key, 0);
                    }
                }
            }

            Refresh();
            Repaint();
        }
        private static bool IsInBranch(string key, string branchKey) =>
            string.Equals(key, branchKey, StringComparison.Ordinal)
            || (key.Length > branchKey.Length
                && key.StartsWith(branchKey, StringComparison.Ordinal)
                && key[branchKey.Length] == '/');

        private void Refresh()
        {
            NotificationDots.GetSnapshot(snapshots, includeInactive);
        }

        private static string GetDefinitionTooltip(string key)
        {
            if (!NotificationDots.TryGetDefinition(key, out NotificationDotDefinition definition))
                return key;

            string view = string.IsNullOrWhiteSpace(definition.ViewKey) ? "None" : definition.ViewKey;
            string mode = definition.ClearsOnVisit ? "On Visit" : "Manual";
            return $"{key}\nMode: {mode}\nView Key: {view}\nDependencies: {definition.Dependencies.Count}";
        }

        private bool MatchesSearch(string key) =>
            string.IsNullOrWhiteSpace(search)
            || key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static int GetDepth(string key)
        {
            int depth = 0;
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] == '/')
                    depth++;
            }

            return depth;
        }

        private static string GetLeafName(string key)
        {
            int index = key.LastIndexOf('/');
            return index < 0 ? key : key.Substring(index + 1);
        }
    }
}
