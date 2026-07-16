using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.Editors.StatSystem
{
    public sealed class StatDatabaseWindow : EditorWindow
    {
        private const string StylePath = "Assets/Framework/Editor/StatSystem/StatDatabaseWindow.uss";
        private const string LastDatabaseKey = "PJDev.StatSystem.LastDatabase";

        private readonly List<StatSO> allStats = new();
        private readonly List<StatSO> filteredStats = new();
        private readonly List<ObjectStatSystem> runtimeSystems = new();
        private readonly List<Stat> runtimeStats = new();

        private VisualElement content;
        private ToolbarToggle dataTab;
        private ToolbarToggle runtimeTab;
        private ObjectField databaseField;
        private ToolbarSearchField searchField;
        private ListView statList;
        private ListView systemList;
        private ListView runtimeStatList;
        private VisualElement inspectorHost;
        private Label dataStatus;
        private Label runtimeStatus;

        private StatDatabaseSO database;
        private StatSO selectedStat;
        private ObjectStatSystem selectedSystem;
        private Editor selectedEditor;
        private bool showingRuntime;
        private bool refreshQueued;
        private bool structureRefreshQueued;

        [MenuItem("PJDev/Stat System/Database", priority = -9600)]
        public static void Open()
        {
            var window = GetWindow<StatDatabaseWindow>();
            window.titleContent = new GUIContent("Stat Database");
            window.minSize = new Vector2(620f, 400f);
            window.Show();
        }

        public static void Open(StatDatabaseSO statDatabase)
        {
            Open();
            var window = GetWindow<StatDatabaseWindow>();
            window.SetDatabase(statDatabase);
        }

        private void OnEnable()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("stat-window");

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null)
                rootVisualElement.styleSheets.Add(style);

            rootVisualElement.Add(BuildHeader());
            content = new VisualElement { name = "content" };
            content.AddToClassList("stat-content");
            rootVisualElement.Add(content);

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.schedule.Execute(RefreshScheduledView).Every(500);

            if (database == null)
                SetDatabase(LoadLastDatabase(), rebuild: false);

            ShowDataTab();
        }

        private VisualElement BuildHeader()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("stat-toolbar");

            dataTab = new ToolbarToggle { text = "Data", value = true };
            runtimeTab = new ToolbarToggle { text = "Runtime" };
            dataTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    ShowDataTab();
                else if (!runtimeTab.value)
                    dataTab.SetValueWithoutNotify(true);
            });
            runtimeTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    ShowRuntimeTab();
                else if (!dataTab.value)
                    runtimeTab.SetValueWithoutNotify(true);
            });

            toolbar.Add(dataTab);
            toolbar.Add(runtimeTab);
            toolbar.Add(new VisualElement { style = { flexGrow = 1f } });
            return toolbar;
        }

        private void ShowDataTab()
        {
            if (content == null)
                return;

            showingRuntime = false;
            dataTab?.SetValueWithoutNotify(true);
            runtimeTab?.SetValueWithoutNotify(false);
            content.Clear();

            content.Add(BuildDataControls());

            var split = new TwoPaneSplitView(0, 290, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("stat-split");
            split.Add(BuildStatList());

            inspectorHost = new VisualElement();
            inspectorHost.AddToClassList("stat-inspector");
            split.Add(inspectorHost);
            content.Add(split);

            RefreshData();
        }

        private VisualElement BuildDataControls()
        {
            var controls = new VisualElement();
            controls.AddToClassList("stat-controls");

            var databaseRow = new VisualElement();
            databaseRow.AddToClassList("stat-row");

            databaseField = new ObjectField("Database")
            {
                objectType = typeof(StatDatabaseSO),
                allowSceneObjects = false,
                value = database
            };
            databaseField.AddToClassList("stat-database-field");
            databaseField.RegisterValueChangedCallback(evt =>
                SetDatabase(evt.newValue as StatDatabaseSO));
            databaseRow.Add(databaseField);
            databaseRow.Add(CreateButton("New Database", CreateDatabase));
            databaseRow.Add(CreateButton("New Stat", CreateStat));
            controls.Add(databaseRow);

            var searchRow = new VisualElement();
            searchRow.AddToClassList("stat-row");
            searchField = new ToolbarSearchField();
            searchField.AddToClassList("stat-search");
            searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            searchRow.Add(searchField);
            searchRow.Add(CreateButton("Remove", RemoveSelectedStat));

            dataStatus = new Label();
            dataStatus.AddToClassList("stat-status");
            searchRow.Add(dataStatus);
            controls.Add(searchRow);
            return controls;
        }

        private VisualElement BuildStatList()
        {
            statList = new ListView
            {
                itemsSource = filteredStats,
                fixedItemHeight = 38f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeStatRow,
                bindItem = BindStatRow,
                style = { flexGrow = 1f }
            };
            statList.AddToClassList("stat-list");
            statList.selectionChanged += OnStatSelectionChanged;
            statList.itemsChosen += _ =>
            {
                if (selectedStat != null)
                {
                    Selection.activeObject = selectedStat;
                    EditorGUIUtility.PingObject(selectedStat);
                }
            };
            return statList;
        }

        private static VisualElement MakeStatRow()
        {
            var row = new VisualElement();
            row.AddToClassList("stat-item");

            var icon = new Image
            {
                name = "icon",
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.AddToClassList("stat-item-icon");
            row.Add(icon);

            var text = new VisualElement();
            text.AddToClassList("stat-item-text");

            var name = new Label { name = "name" };
            name.AddToClassList("stat-item-name");
            text.Add(name);

            var range = new Label { name = "range" };
            range.AddToClassList("stat-item-range");
            text.Add(range);

            row.Add(text);
            return row;
        }

        private void BindStatRow(VisualElement row, int index)
        {
            if (index < 0 || index >= filteredStats.Count)
                return;

            StatSO stat = filteredStats[index];
            Image icon = row.Q<Image>("icon");
            icon.sprite = stat.StatIcon;
            icon.style.visibility =
                stat.StatIcon != null ? Visibility.Visible : Visibility.Hidden;

            row.Q<Label>("name").text =
                string.IsNullOrEmpty(stat.DisplayName) ? stat.StatName : stat.DisplayName;
            row.Q<Label>("range").text =
                $"{stat.StatName}  |  {stat.BaseValue:0.###}  [{stat.MinValue:0.###}, {stat.MaxValue:0.###}]";
            row.tooltip = AssetDatabase.GetAssetPath(stat);
        }

        private void OnStatSelectionChanged(IEnumerable<object> selection)
        {
            selectedStat = null;
            foreach (object item in selection)
            {
                selectedStat = item as StatSO;
                break;
            }

            ShowSelectedInspector();
        }

        private void ShowSelectedInspector()
        {
            inspectorHost?.Clear();
            DestroySelectedEditor();

            if (selectedStat == null)
            {
                var empty = new Label("Select a Stat to edit.");
                empty.AddToClassList("stat-empty");
                inspectorHost?.Add(empty);
                return;
            }

            selectedEditor = Editor.CreateEditor(selectedStat);
            inspectorHost?.Add(new InspectorElement(selectedEditor));
        }

        private void RefreshData()
        {
            allStats.Clear();
            if (database != null)
            {
                StatSO[] source = database.Stats;
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != null)
                        allStats.Add(source[i]);
                }
            }

            if (selectedStat != null && !allStats.Contains(selectedStat))
            {
                selectedStat = null;
                ShowSelectedInspector();
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            filteredStats.Clear();
            string search = searchField?.value?.Trim();
            bool hasSearch = !string.IsNullOrEmpty(search);

            for (int i = 0; i < allStats.Count; i++)
            {
                StatSO stat = allStats[i];
                if (!hasSearch ||
                    stat.StatName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrEmpty(stat.DisplayName) &&
                     stat.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    filteredStats.Add(stat);
                }
            }

            statList?.Rebuild();
            if (dataStatus != null)
            {
                dataStatus.text = database == null
                    ? "Select or create a database."
                    : $"{filteredStats.Count} / {allStats.Count} Stats";
            }
        }

        private void SetDatabase(StatDatabaseSO value, bool rebuild = true)
        {
            database = value;
            databaseField?.SetValueWithoutNotify(value);
            SaveLastDatabase(value);

            if (rebuild && !showingRuntime)
                RefreshData();
        }

        private void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Stat Database",
                "SO_StatDatabase",
                "asset",
                "Choose where to create the Stat Database.");

            if (string.IsNullOrEmpty(path))
                return;

            var asset = CreateInstance<StatDatabaseSO>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SetDatabase(asset);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void CreateStat()
        {
            if (database == null)
            {
                EditorUtility.DisplayDialog("Stat Database", "Select or create a database first.", "Close");
                return;
            }

            string databasePath = AssetDatabase.GetAssetPath(database);
            string directory = Path.GetDirectoryName(databasePath)?.Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Stat",
                "SO_New_Stat",
                "asset",
                "Choose where to create the Stat asset.",
                directory);

            if (string.IsNullOrEmpty(path))
                return;

            string statName = Path.GetFileNameWithoutExtension(path);
            if (statName.StartsWith("SO_", StringComparison.OrdinalIgnoreCase))
                statName = statName.Substring(3);
            if (statName.EndsWith("_Stat", StringComparison.OrdinalIgnoreCase))
                statName = statName.Substring(0, statName.Length - 5);
            if (string.IsNullOrWhiteSpace(statName))
                statName = "NewStat";

            var stat = CreateInstance<StatSO>();
            var serializedStat = new SerializedObject(stat);
            serializedStat.FindProperty("statName").stringValue = statName;
            serializedStat.FindProperty("displayName").stringValue = statName;
            serializedStat.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(stat, path);
            AddStatToDatabase(stat);
            AssetDatabase.SaveAssets();

            selectedStat = stat;
            RefreshData();
            int index = filteredStats.IndexOf(stat);
            if (index >= 0)
                statList.SetSelection(index);

            Selection.activeObject = stat;
            EditorGUIUtility.PingObject(stat);
        }

        private void AddStatToDatabase(StatSO stat)
        {
            Undo.RecordObject(database, "Add Stat");
            var serializedDatabase = new SerializedObject(database);
            SerializedProperty stats = serializedDatabase.FindProperty("stats");
            int index = stats.arraySize;
            stats.InsertArrayElementAtIndex(index);
            stats.GetArrayElementAtIndex(index).objectReferenceValue = stat;
            serializedDatabase.ApplyModifiedProperties();
            database.RebuildCache();
            EditorUtility.SetDirty(database);
        }

        private void RemoveSelectedStat()
        {
            if (database == null || selectedStat == null)
                return;

            Undo.RecordObject(database, "Remove Stat");
            var serializedDatabase = new SerializedObject(database);
            SerializedProperty stats = serializedDatabase.FindProperty("stats");

            for (int i = 0; i < stats.arraySize; i++)
            {
                if (stats.GetArrayElementAtIndex(i).objectReferenceValue != selectedStat)
                    continue;

                int previousSize = stats.arraySize;
                stats.DeleteArrayElementAtIndex(i);
                if (stats.arraySize == previousSize)
                    stats.DeleteArrayElementAtIndex(i);
                break;
            }

            serializedDatabase.ApplyModifiedProperties();
            database.RebuildCache();
            EditorUtility.SetDirty(database);
            selectedStat = null;
            RefreshData();
            ShowSelectedInspector();
        }

        private void ShowRuntimeTab()
        {
            if (content == null)
                return;

            showingRuntime = true;
            dataTab?.SetValueWithoutNotify(false);
            runtimeTab?.SetValueWithoutNotify(true);
            content.Clear();

            runtimeStatus = new Label();
            runtimeStatus.AddToClassList("stat-runtime-status");
            content.Add(runtimeStatus);

            var split = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("stat-split");

            systemList = new ListView
            {
                itemsSource = runtimeSystems,
                fixedItemHeight = 32f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new Label(),
                bindItem = BindSystemRow,
                style = { flexGrow = 1f }
            };
            systemList.AddToClassList("stat-list");
            systemList.selectionChanged += OnSystemSelectionChanged;
            split.Add(systemList);

            runtimeStatList = new ListView
            {
                itemsSource = runtimeStats,
                fixedItemHeight = 48f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                makeItem = MakeRuntimeStatRow,
                bindItem = BindRuntimeStatRow,
                style = { flexGrow = 1f }
            };
            runtimeStatList.AddToClassList("stat-list");
            split.Add(runtimeStatList);
            content.Add(split);

            RefreshRuntimeSystems();
        }

        private void RefreshScheduledView()
        {
            if (!showingRuntime)
            {
                statList?.RefreshItems();
                return;
            }

            if (runtimeStatus == null)
                return;

            if (!EditorApplication.isPlaying)
            {
                bool hadSystems = runtimeSystems.Count > 0;
                bool hadStats = runtimeStats.Count > 0;
                runtimeSystems.Clear();
                runtimeStats.Clear();

                if (hadSystems)
                    systemList?.Rebuild();
                if (hadStats)
                    runtimeStatList?.Rebuild();

                runtimeStatus.text = "Runtime monitoring is available in Play Mode.";
                return;
            }

            RefreshRuntimeSystems();
            RefreshSelectedRuntimeStats();
        }

        private void RefreshRuntimeSystems()
        {
            if (!EditorApplication.isPlaying)
            {
                runtimeStatus.text = "Runtime monitoring is available in Play Mode.";
                return;
            }

            ObjectStatSystem[] found =
                Object.FindObjectsByType<ObjectStatSystem>(FindObjectsInactive.Include);

            bool changed = found.Length != runtimeSystems.Count;
            if (!changed)
            {
                for (int i = 0; i < found.Length; i++)
                {
                    if (runtimeSystems[i] != found[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                runtimeSystems.Clear();
                runtimeSystems.AddRange(found);
                if (selectedSystem != null && !runtimeSystems.Contains(selectedSystem))
                    selectedSystem = null;
                systemList?.Rebuild();
            }

            runtimeStatus.text =
                $"{runtimeSystems.Count} Object Stat System(s)  |  Refresh 0.5s";
        }

        private void BindSystemRow(VisualElement element, int index)
        {
            if (index < 0 || index >= runtimeSystems.Count)
                return;

            ObjectStatSystem system = runtimeSystems[index];
            ((Label)element).text =
                $"{system.name}  ({system.StatCollection.Count})";
            element.tooltip = GetHierarchyPath(system.transform);
        }

        private void OnSystemSelectionChanged(IEnumerable<object> selection)
        {
            selectedSystem = null;
            foreach (object item in selection)
            {
                selectedSystem = item as ObjectStatSystem;
                break;
            }

            RefreshSelectedRuntimeStats();
        }

        private void RefreshSelectedRuntimeStats()
        {
            int expectedCount = selectedSystem != null ? selectedSystem.StatCollection.Count : 0;
            bool structureChanged = runtimeStats.Count != expectedCount;

            if (!structureChanged && selectedSystem != null)
            {
                int index = 0;
                foreach (Stat stat in selectedSystem.StatCollection)
                {
                    if (!ReferenceEquals(runtimeStats[index], stat))
                    {
                        structureChanged = true;
                        break;
                    }

                    index++;
                }
            }

            if (structureChanged)
            {
                runtimeStats.Clear();
                if (selectedSystem != null)
                {
                    foreach (Stat stat in selectedSystem.StatCollection)
                        runtimeStats.Add(stat);
                }

                runtimeStatList?.Rebuild();
                return;
            }

            runtimeStatList?.RefreshItems();
        }

        private static VisualElement MakeRuntimeStatRow()
        {
            var row = new VisualElement();
            row.AddToClassList("stat-runtime-item");

            var icon = new Image
            {
                name = "icon",
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.AddToClassList("stat-item-icon");
            row.Add(icon);

            var text = new VisualElement();
            text.AddToClassList("stat-item-text");
            text.Add(new Label { name = "name" });
            text.Add(new Label { name = "value" });
            row.Add(text);
            return row;
        }

        private void BindRuntimeStatRow(VisualElement row, int index)
        {
            if (index < 0 || index >= runtimeStats.Count)
                return;

            Stat stat = runtimeStats[index];
            Image icon = row.Q<Image>("icon");
            icon.sprite = stat.StatIcon;
            icon.style.visibility =
                stat.StatIcon != null ? Visibility.Visible : Visibility.Hidden;

            row.Q<Label>("name").text =
                string.IsNullOrEmpty(stat.DisplayName) ? stat.StatName : stat.DisplayName;
            row.Q<Label>("value").text =
                $"Value {stat.Value:0.###}  |  Base {stat.BaseValue:0.###}  |  Modifiers {stat.ModifierCount}";
        }

        private UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            bool refreshValues = false;
            bool refreshStructure = false;

            for (int i = 0; i < modifications.Length; i++)
            {
                Object target = modifications[i].currentValue.target;
                if (target == database)
                {
                    refreshStructure = true;
                    break;
                }

                if (target is StatSO stat &&
                    (ReferenceEquals(stat, selectedStat) || allStats.Contains(stat)))
                {
                    refreshValues = true;
                }
            }

            if (refreshStructure || refreshValues)
                QueueDataRefresh(refreshStructure);

            return modifications;
        }

        private void OnUndoRedo() => QueueDataRefresh(structureChanged: true);

        private void OnProjectChanged() => QueueDataRefresh(structureChanged: true);

        private void QueueDataRefresh(bool structureChanged)
        {
            structureRefreshQueued |= structureChanged;
            if (refreshQueued)
                return;

            refreshQueued = true;
            EditorApplication.delayCall += ApplyQueuedDataRefresh;
        }

        private void ApplyQueuedDataRefresh()
        {
            refreshQueued = false;
            bool refreshStructure = structureRefreshQueued;
            structureRefreshQueued = false;

            if (this == null)
                return;

            database?.RebuildCache();

            if (showingRuntime)
                return;

            if (refreshStructure)
                RefreshData();
            else
                statList?.RefreshItems();

            if (selectedEditor != null)
            {
                selectedEditor.serializedObject.UpdateIfRequiredOrScript();
                selectedEditor.Repaint();
            }

            inspectorHost?.MarkDirtyRepaint();
            Repaint();
        }
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!showingRuntime && evt.keyCode == KeyCode.Delete)
            {
                RemoveSelectedStat();
                evt.StopPropagation();
            }
            else if (!showingRuntime && evt.ctrlKey && evt.keyCode == KeyCode.F)
            {
                searchField?.Focus();
                evt.StopPropagation();
            }
            else if (!showingRuntime && evt.keyCode == KeyCode.Escape &&
                     !string.IsNullOrEmpty(searchField?.value))
            {
                searchField.value = string.Empty;
                evt.StopPropagation();
            }
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("stat-button");
            return button;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void SaveLastDatabase(StatDatabaseSO value)
        {
            if (value == null)
            {
                EditorPrefs.DeleteKey(LastDatabaseKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(value);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                EditorPrefs.SetString(LastDatabaseKey, guid);
        }

        private static StatDatabaseSO LoadLastDatabase()
        {
            string guid = EditorPrefs.GetString(LastDatabaseKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<StatDatabaseSO>(path);
        }

        private void DestroySelectedEditor()
        {
            if (selectedEditor == null)
                return;

            DestroyImmediate(selectedEditor);
            selectedEditor = null;
        }

        private void OnDisable()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.delayCall -= ApplyQueuedDataRefresh;
            refreshQueued = false;
            structureRefreshQueued = false;
            DestroySelectedEditor();
        }
    }

    internal static class StatDatabaseOpenHandler
    {
        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not StatDatabaseSO database)
                return false;

            StatDatabaseWindow.Open(database);
            return true;
        }
    }
}