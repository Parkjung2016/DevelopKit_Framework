using System.Collections.Generic;
using PJDev.DevelopKit.Framework.PoolSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.PoolSystem
{
    public sealed class PoolMonitorWindow : EditorWindow
    {
        private const string StylePath = "Assets/Framework/Editor/PoolSystem/PoolMonitorWindow.uss";

        private readonly List<PrefabPoolStats> stats = new();

        private ListView list;
        private Label status;
        private Button clearButton;

        [MenuItem("PJDev/Pool System/Monitor", priority = -9550)]
        public static void Open()
        {
            var window = GetWindow<PoolMonitorWindow>();
            window.titleContent = new GUIContent("Pool Monitor");
            window.minSize = new Vector2(520f, 300f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("pool-monitor");

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null)
                rootVisualElement.styleSheets.Add(style);

            rootVisualElement.Add(BuildToolbar());
            rootVisualElement.Add(BuildHeader());

            list = new ListView
            {
                itemsSource = stats,
                fixedItemHeight = 34f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                style = { flexGrow = 1f }
            };
            list.itemsChosen += selection =>
            {
                foreach (object item in selection)
                {
                    if (item is PrefabPoolStats selected && selected.Prefab != null)
                    {
                        Selection.activeObject = selected.Prefab;
                        EditorGUIUtility.PingObject(selected.Prefab);
                    }

                    break;
                }
            };
            rootVisualElement.Add(list);

            rootVisualElement.schedule.Execute(Refresh).Every(500);
            Refresh();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("pool-toolbar");

            status = new Label("Play Mode required");
            status.AddToClassList("pool-status");
            toolbar.Add(status);

            clearButton = new Button(ClearInactive) { text = "Clear Inactive" };
            clearButton.AddToClassList("pool-button");
            toolbar.Add(clearButton);
            return toolbar;
        }

        private static VisualElement BuildHeader()
        {
            var row = new VisualElement();
            row.AddToClassList("pool-header");
            row.Add(CreateLabel("Prefab", "prefab"));
            row.Add(CreateLabel("Active", "number"));
            row.Add(CreateLabel("Inactive", "number"));
            row.Add(CreateLabel("Total", "number"));
            row.Add(CreateLabel("Max", "number"));
            return row;
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("pool-row");

            var prefabCell = new VisualElement();
            prefabCell.AddToClassList("pool-prefab-cell");

            var icon = new Image
            {
                name = "icon",
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.AddToClassList("pool-icon");
            prefabCell.Add(icon);

            var name = new Label { name = "prefab" };
            name.AddToClassList("pool-name");
            prefabCell.Add(name);
            row.Add(prefabCell);

            row.Add(CreateLabel(string.Empty, "number", "active"));
            row.Add(CreateLabel(string.Empty, "number", "inactive"));
            row.Add(CreateLabel(string.Empty, "number", "total"));
            row.Add(CreateLabel(string.Empty, "number", "max"));
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= stats.Count)
                return;

            PrefabPoolStats value = stats[index];
            row.Q<Label>("prefab").text =
                value.Prefab != null ? value.Prefab.name : "Missing Prefab";
            row.Q<Label>("active").text = value.CountActive.ToString();
            row.Q<Label>("inactive").text = value.CountInactive.ToString();
            row.Q<Label>("total").text = value.CountAll.ToString();
            row.Q<Label>("max").text = value.MaxSize.ToString();

            Image icon = row.Q<Image>("icon");
            icon.image = value.Prefab != null
                ? AssetPreview.GetMiniThumbnail(value.Prefab)
                : null;
            row.tooltip = value.Prefab != null
                ? AssetDatabase.GetAssetPath(value.Prefab)
                : string.Empty;
        }

        private void Refresh()
        {
            if (!EditorApplication.isPlaying)
            {
                if (stats.Count > 0)
                {
                    stats.Clear();
                    list?.Rebuild();
                }

                status.text = "Runtime pools are shown in Play Mode.";
                clearButton?.SetEnabled(false);
                return;
            }

            int previousCount = stats.Count;
            PrefabPool.GetStats(stats);

            if (previousCount == stats.Count)
                list?.RefreshItems();
            else
                list?.Rebuild();

            int active = 0;
            int inactive = 0;
            for (int i = 0; i < stats.Count; i++)
            {
                active += stats[i].CountActive;
                inactive += stats[i].CountInactive;
            }

            status.text =
                $"{stats.Count} pools  |  {active} active  |  {inactive} inactive";
            clearButton?.SetEnabled(stats.Count > 0);
        }

        private void ClearInactive()
        {
            if (!EditorApplication.isPlaying)
                return;

            PrefabPool.ClearInactive();
            Refresh();
        }

        private static Label CreateLabel(string text, string className, string name = null)
        {
            var label = new Label(text) { name = name };
            label.AddToClassList(className);
            return label;
        }
    }

    [CustomEditor(typeof(PrefabPoolSettingsSO))]
    internal sealed class PrefabPoolSettingsSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var settings = (PrefabPoolSettingsSO)target;
            DrawValidation(settings);

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Prewarm Now", GUILayout.Height(24f)))
                    settings.Prewarm();
            }

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Prewarm Now is available in Play Mode.", MessageType.Info);
        }

        private static void DrawValidation(PrefabPoolSettingsSO settings)
        {
            var prefabs = new HashSet<GameObject>();
            int missing = 0;
            int duplicate = 0;

            IReadOnlyList<PrefabPoolConfig> pools = settings.Pools;
            for (int i = 0; i < pools.Count; i++)
            {
                PrefabPoolConfig config = pools[i];
                if (config?.Prefab == null)
                {
                    missing++;
                    continue;
                }

                if (!prefabs.Add(config.Prefab))
                    duplicate++;
            }

            if (missing > 0)
                EditorGUILayout.HelpBox($"{missing} missing prefab reference(s).", MessageType.Warning);
            if (duplicate > 0)
                EditorGUILayout.HelpBox($"{duplicate} duplicate prefab configuration(s).", MessageType.Error);
        }
    }
}