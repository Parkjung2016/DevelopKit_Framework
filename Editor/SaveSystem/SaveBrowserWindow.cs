using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using PJDev.DevelopKit.Framework.SaveSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.SaveSystem
{
    public sealed class SaveBrowserWindow : EditorWindow
    {
        private const string StyleGuid = "deb6d2fa55c343858a559ccf14d03d08";
        private static StyleSheet cachedStyleSheet;

        private readonly List<SaveBrowserEntry> entries = new();
        private readonly List<SaveBrowserEntry> filteredEntries = new();

        private ObjectField settingsField;
        private TextField directoryField;
        private TextField extensionField;
        private ToolbarSearchField searchField;
        private ListView listView;
        private Label statusLabel;
        private Label countLabel;
        private Label detailsTitle;
        private Label detailsLine;
        private Button refreshButton;

        private SaveBrowserEntry selectedEntry;
        private SaveSettingsSO pendingSettings;
        private CancellationTokenSource scanCancellation;
        private int scanVersion;

        [MenuItem("PJDev/Save System/Slot Browser", priority = -9650)]
        public static void Open()
        {
            var window = GetWindow<SaveBrowserWindow>();
            window.titleContent = new GUIContent("Save Slots");
            window.minSize = new Vector2(560, 360);
            window.Show();
        }

        public static void Open(SaveSettingsSO settings)
        {
            Open();
            var window = GetWindow<SaveBrowserWindow>();
            window.pendingSettings = settings;
            window.ApplySettings(settings);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("save-browser");

            StyleSheet styleSheet = GetStyleSheet();
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            rootVisualElement.Add(BuildHeader());
            rootVisualElement.Add(BuildConfiguration());
            rootVisualElement.Add(BuildSearch());
            rootVisualElement.Add(BuildListHeader());

            listView = new ListView
            {
                itemsSource = filteredEntries,
                fixedItemHeight = 26,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                style = { flexGrow = 1 }
            };
            listView.selectionChanged += OnSelectionChanged;
            listView.itemsChosen += _ => RevealSelected();
            listView.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
            rootVisualElement.Add(listView);
            rootVisualElement.Add(BuildDetails());

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

            pendingSettings ??= SaveBrowserSession.LoadLastSettings();

            if (pendingSettings != null)
                ApplySettings(pendingSettings, refresh: false);
            else
                ApplyDefaultPath();

            RefreshEntries();
        }

        private static StyleSheet GetStyleSheet()
        {
            if (cachedStyleSheet != null)
                return cachedStyleSheet;

            string path = AssetDatabase.GUIDToAssetPath(StyleGuid);
            if (string.IsNullOrEmpty(path))
                return null;

            cachedStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            return cachedStyleSheet;
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("save-header");

            var title = new Label("Save Slot Browser");
            title.AddToClassList("save-title");
            header.Add(title);

            statusLabel = new Label("Ready");
            statusLabel.AddToClassList("save-status");
            header.Add(statusLabel);
            return header;
        }

        private VisualElement BuildConfiguration()
        {
            var container = new VisualElement();
            container.AddToClassList("save-config");

            var settingsRow = new VisualElement();
            settingsRow.AddToClassList("save-config-row");

            settingsField = new ObjectField("Settings")
            {
                objectType = typeof(SaveSettingsSO),
                allowSceneObjects = false
            };
            settingsField.AddToClassList("save-settings-field");
            settingsField.RegisterValueChangedCallback(
                evt => ApplySettings(evt.newValue as SaveSettingsSO));
            settingsRow.Add(settingsField);

            settingsRow.Add(CreateButton("New Settings", CreateSettingsAsset));
            container.Add(settingsRow);

            var pathRow = new VisualElement();
            pathRow.AddToClassList("save-config-row");

            directoryField = new TextField("Directory") { isReadOnly = true };
            directoryField.AddToClassList("save-path-field");
            pathRow.Add(directoryField);

            extensionField = new TextField("Extension") { isReadOnly = true };
            extensionField.AddToClassList("save-extension-field");
            pathRow.Add(extensionField);
            pathRow.Add(CreateButton("Open Folder", OpenDirectory));

            refreshButton = CreateButton("Refresh", RefreshEntries);
            pathRow.Add(refreshButton);

            container.Add(pathRow);
            return container;
        }

        private VisualElement BuildSearch()
        {
            var row = new VisualElement();
            row.AddToClassList("save-search-row");

            searchField = new ToolbarSearchField();
            searchField.AddToClassList("save-search");
            searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            row.Add(searchField);

            countLabel = new Label("0 slots");
            countLabel.AddToClassList("save-count");
            row.Add(countLabel);
            return row;
        }

        private static VisualElement BuildListHeader()
        {
            var row = new VisualElement();
            row.AddToClassList("save-list-header");
            row.Add(CreateCell("Status", "save-cell-status"));
            row.Add(CreateCell("Slot", "save-cell-slot"));
            row.Add(CreateCell("Protection", "save-cell-encryption"));
            row.Add(CreateCell("Size", "save-cell-size"));
            row.Add(CreateCell("Modified", "save-cell-date"));
            return row;
        }

        private VisualElement BuildDetails()
        {
            var details = new VisualElement();
            details.AddToClassList("save-details");

            detailsTitle = new Label("No slot selected");
            detailsTitle.AddToClassList("save-details-title");
            details.Add(detailsTitle);

            detailsLine = new Label("Select a slot to see file details.");
            detailsLine.AddToClassList("save-details-line");
            details.Add(detailsLine);
            return details;
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("save-row");
            row.Add(CreateCell(string.Empty, "save-cell-status", "status"));
            row.Add(CreateCell(string.Empty, "save-cell-slot", "slot"));
            row.Add(CreateCell(string.Empty, "save-cell-encryption", "encryption"));
            row.Add(CreateCell(string.Empty, "save-cell-size", "size"));
            row.Add(CreateCell(string.Empty, "save-cell-date", "date"));
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= filteredEntries.Count)
                return;

            SaveBrowserEntry entry = filteredEntries[index];
            Label status = row.Q<Label>("status");
            status.text = entry.IsValid ? "Valid" : "Invalid";
            status.EnableInClassList("save-valid", entry.IsValid);
            status.EnableInClassList("save-invalid", !entry.IsValid);

            row.Q<Label>("slot").text = entry.SlotId;
            row.Q<Label>("encryption").text = entry.IsValid
                ? entry.Metadata.IsEncrypted ? "Encrypted" : "Plain"
                : "-";
            row.Q<Label>("size").text = FormatBytes(entry.FileSize);
            row.Q<Label>("date").text = entry.ModifiedAt.ToString("yyyy-MM-dd HH:mm");
            row.tooltip = entry.Error ?? entry.FilePath;
        }

        private async void RefreshEntries()
        {
            int version = ++scanVersion;
            scanCancellation?.Cancel();
            scanCancellation?.Dispose();
            scanCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = scanCancellation.Token;

            if (!TryGetScanSettings(out string directory, out string extension, out string error))
            {
                entries.Clear();
                ApplyFilter();
                SetStatus(error, false);
                return;
            }

            SetStatus("Scanning...", true);

            try
            {
                SaveBrowserScanResult result = await SaveBrowserScanner.ScanAsync(
                    directory,
                    extension,
                    cancellationToken);

                if (this == null || version != scanVersion || cancellationToken.IsCancellationRequested)
                    return;

                entries.Clear();
                entries.AddRange(result.Entries);
                ApplyFilter();
                SetStatus(
                    string.IsNullOrEmpty(result.Error) ? "Ready" : result.Error,
                    false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (this != null && version == scanVersion)
                    refreshButton?.SetEnabled(true);
            }
        }

        private void ApplyFilter()
        {
            filteredEntries.Clear();
            string search = searchField?.value?.Trim();
            bool hasSearch = !string.IsNullOrEmpty(search);

            for (int i = 0; i < entries.Count; i++)
            {
                SaveBrowserEntry entry = entries[i];
                if (!hasSearch
                    || entry.SlotId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filteredEntries.Add(entry);
                }
            }

            selectedEntry = null;
            listView?.ClearSelection();
            listView?.Rebuild();
            countLabel.text = $"{filteredEntries.Count} / {entries.Count} slots";
            UpdateDetails();
        }

        private void OnSelectionChanged(IEnumerable<object> selectedItems)
        {
            selectedEntry = null;
            foreach (object item in selectedItems)
            {
                selectedEntry = item as SaveBrowserEntry;
                break;
            }

            UpdateDetails();
        }

        private void UpdateDetails()
        {
            if (selectedEntry == null)
            {
                detailsTitle.text = "No slot selected";
                detailsLine.text = "Select a slot to see file details.";
                return;
            }

            detailsTitle.text = selectedEntry.SlotId;
            if (!selectedEntry.IsValid)
            {
                detailsLine.text =
                    $"Invalid file | {FormatBytes(selectedEntry.FileSize)} | {selectedEntry.Error}";
                return;
            }

            string protection = selectedEntry.Metadata.IsEncrypted ? "Encrypted" : "Plain";
            detailsLine.text =
                $"Version {selectedEntry.Metadata.Version} | {protection} | " +
                $"Payload {FormatBytes(selectedEntry.Metadata.PayloadSize)} | " +
                $"File {FormatBytes(selectedEntry.FileSize)} | " +
                $"CRC 0x{selectedEntry.Metadata.Checksum:X8}\n{selectedEntry.FilePath}";
        }

        private void BuildContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction(
                "Reveal in Explorer",
                _ => RevealSelected(),
                _ => selectedEntry != null
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(
                "Delete",
                _ => DeleteSelected(),
                _ => selectedEntry != null
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.F5)
            {
                RefreshEntries();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Delete)
            {
                DeleteSelected();
                evt.StopPropagation();
            }
            else if (evt.ctrlKey && evt.keyCode == KeyCode.F)
            {
                searchField?.Focus();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape && !string.IsNullOrEmpty(searchField?.value))
            {
                searchField.value = string.Empty;
                evt.StopPropagation();
            }
        }

        private void ApplySettings(SaveSettingsSO settings, bool refresh = true)
        {
            pendingSettings = settings;
            SaveBrowserSession.SaveLastSettings(settings);
            if (settingsField != null)
                settingsField.SetValueWithoutNotify(settings);

            if (directoryField == null || extensionField == null)
                return;

            if (settings == null)
            {
                ApplyDefaultPath();
                if (refresh)
                    RefreshEntries();
                return;
            }

            try
            {
                LocalFileSaveStorage storage = settings.CreateStorage();

                directoryField.SetValueWithoutNotify(storage.SaveDirectory);
                extensionField.SetValueWithoutNotify(storage.FileExtension);
                if (refresh)
                    RefreshEntries();
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, false);
            }
        }

        private void ApplyDefaultPath()
        {
            if (directoryField == null || extensionField == null)
                return;

            var storage = new LocalFileSaveStorage();
            directoryField.SetValueWithoutNotify(storage.SaveDirectory);
            extensionField.SetValueWithoutNotify(storage.FileExtension);
        }

        private bool TryGetScanSettings(
            out string directory,
            out string extension,
            out string error)
        {
            directory = null;
            extension = null;
            error = null;

            try
            {
                var storage = new LocalFileSaveStorage(
                    directoryField?.value,
                    extensionField?.value);

                directory = storage.SaveDirectory;
                extension = storage.FileExtension;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private void OpenDirectory()
        {
            if (!TryGetScanSettings(out string directory, out _, out string error))
            {
                SetStatus(error, false);
                return;
            }

            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory);
        }

        private void RevealSelected()
        {
            if (selectedEntry != null && File.Exists(selectedEntry.FilePath))
                EditorUtility.RevealInFinder(selectedEntry.FilePath);
        }

        private void DeleteSelected()
        {
            if (selectedEntry == null)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Save Slot",
                $"Delete '{selectedEntry.SlotId}'?\n\n{selectedEntry.FilePath}",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            try
            {
                File.Delete(selectedEntry.FilePath);
                selectedEntry = null;
                RefreshEntries();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Delete Failed", exception.Message, "Close");
            }
        }

        private void CreateSettingsAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Save Settings",
                "SaveSettings",
                "asset",
                "Choose where to create the SaveSettingsSO asset.");

            if (string.IsNullOrEmpty(path))
                return;

            var settings = CreateInstance<SaveSettingsSO>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            settingsField.value = settings;
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private void SetStatus(string text, bool busy)
        {
            statusLabel.text = text;
            refreshButton?.SetEnabled(!busy);
        }

        private void OnDisable()
        {
            scanVersion++;
            scanCancellation?.Cancel();
            scanCancellation?.Dispose();
            scanCancellation = null;
        }

        private static Label CreateCell(string text, string className, string name = null)
        {
            var label = new Label(text) { name = name };
            label.AddToClassList(className);
            return label;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("save-button");
            return button;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";

            if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:0.#} KB";

            return $"{bytes / (1024f * 1024f):0.#} MB";
        }
    }
}