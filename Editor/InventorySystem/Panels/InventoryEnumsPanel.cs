using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels
{
    internal sealed class InventoryEnumsPanel : InventoryEditorPanelBase
    {
        private enum EnumCategory
        {
            ItemType,
            ContainerKind
        }

        private InventoryEnumsDocument document;
        private EnumCategory category = EnumCategory.ItemType;
        private int selectedIndex = -1;
        private VisualElement listHost;
        private VisualElement detailHost;
        private Label statusLabel;

        public InventoryEnumsPanel(InventoryEditorContext context) : base(context) { }

        public override string Title => "Enums";

        public override void Refresh()
        {
            Root.Clear();
            document = InventoryEnumSettingsStore.LoadOrCreateDefault();
            selectedIndex = Mathf.Clamp(selectedIndex, -1, GetActiveEntries().Length - 1);

            Root.Add(InventoryInspectorUI.BuildHeader("Inventory Enums"));

            Root.Add(new HelpBox(
                "ItemType · ContainerKind를 편집한 뒤 Generate Enums로 C# enum을 갱신합니다.\n" +
                "Up/Down은 순서와 Value(0부터)를 함께 갱신합니다. Route 테이블의 value 참조도 맞춰집니다.",
                HelpBoxMessageType.None));
            
            Root.Add(BuildToolbar());

            var split = InventoryEditorUIFactory.CreateSplitView(300);
            Root.Add(split);
            (listHost, detailHost) = InventoryEditorUIFactory.GetSplit(split);

            listHost.Add(BuildCategoryTabs());
            listHost.Add(new ScrollView { name = "enum-list-scroll", style = { flexGrow = 1 } });

            statusLabel = new Label { style = { marginTop = 6, opacity = 0.8f, fontSize = 11 } };
            Root.Add(statusLabel);

            RebuildList();
            RebuildDetail();
            UpdateStatus();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList(InventoryEditorStyles.ToolbarClass);
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 6;

            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("+ Add", AddEntry));
            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Remove", RemoveSelected));
            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Up", () => MoveSelected(-1)));
            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Down", () => MoveSelected(1)));
            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Reset Defaults", ResetDefaults));

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            toolbar.Add(spacer);

            var generate = InventoryEditorUIFactory.CreateToolbarButton("Generate Enums", GenerateEnums);
            generate.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(generate);
            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Save", SaveDocument));

            return toolbar;
        }

        private VisualElement categoryTabHost;

        private VisualElement BuildCategoryTabs()
        {
            categoryTabHost = new VisualElement();
            categoryTabHost.style.flexDirection = FlexDirection.Row;
            categoryTabHost.style.marginBottom = 6;
            RefreshCategoryTabs();
            return categoryTabHost;
        }

        private void RefreshCategoryTabs()
        {
            if (categoryTabHost == null)
                return;

            categoryTabHost.Clear();
            categoryTabHost.Add(CreateCategoryButton("ItemType", EnumCategory.ItemType));
            categoryTabHost.Add(CreateCategoryButton("ContainerKind", EnumCategory.ContainerKind));
        }

        private Button CreateCategoryButton(string label, EnumCategory target)
        {
            var button = new Button(() =>
            {
                category = target;
                selectedIndex = -1;
                RefreshCategoryTabs();
                RebuildList();
                RebuildDetail();
                UpdateStatus();
            })
            {
                text = label
            };
            button.AddToClassList(InventoryEditorStyles.NavButtonClass);
            button.EnableInClassList(InventoryEditorStyles.NavButtonActiveClass, category == target);
            button.style.flexGrow = 1;
            return button;
        }

        private InventoryEnumEntryData[] GetActiveEntries() =>
            category == EnumCategory.ItemType ? document.itemTypes : document.containerKinds;

        private void SetActiveEntries(InventoryEnumEntryData[] entries)
        {
            if (category == EnumCategory.ItemType)
                document.itemTypes = entries;
            else
                document.containerKinds = entries;
        }

        private ScrollView ListScroll => listHost?.Q<ScrollView>("enum-list-scroll");

        private bool IsValidSelection()
        {
            InventoryEnumEntryData[] entries = GetActiveEntries();
            return selectedIndex >= 0 && selectedIndex < entries.Length && entries[selectedIndex] != null;
        }

        private InventoryEnumEntryData GetSelected() =>
            IsValidSelection() ? GetActiveEntries()[selectedIndex] : null;

        private void RebuildList()
        {
            ScrollView scroll = ListScroll;
            if (scroll == null)
                return;

            InventoryEditorUIFactory.RunPreserveScroll(scroll, () =>
            {
                scroll.Clear();
                InventoryEnumEntryData[] entries = GetActiveEntries();

                for (int i = 0; i < entries.Length; i++)
                {
                    InventoryEnumEntryData entry = entries[i];
                    if (entry == null)
                        continue;

                    int index = i;
                    var row = new VisualElement();
                    row.AddToClassList(InventoryEditorStyles.ListRowClass);
                    if (index == selectedIndex)
                        row.AddToClassList(InventoryEditorStyles.ListRowSelectedClass);

                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        selectedIndex = index;
                        RebuildList();
                        RebuildDetail();
                    });

                    string primary = entry.name;
                    string secondary = $"value {entry.value}";

                    var inner = new VisualElement();
                    inner.AddToClassList(InventoryEditorStyles.ListRowInnerClass);
                    inner.Add(InventoryEditorVisuals.CreateEmptySlot(InventoryEditorVisuals.SlotSize.Small));
                    var content = new VisualElement();
                    content.AddToClassList(InventoryEditorStyles.ListRowContentClass);
                    content.Add(InventoryEditorVisuals.CreateEllipsisLabel(primary, bold: true));
                    content.Add(InventoryEditorVisuals.CreateEllipsisLabel(secondary, fontSize: 11));
                    inner.Add(content);
                    row.Add(inner);
                    scroll.Add(row);
                }
            });
        }

        private void RebuildDetail()
        {
            detailHost.Clear();
            InventoryEnumEntryData entry = GetSelected();
            if (entry == null)
            {
                detailHost.Add(new HelpBox("왼쪽에서 enum 항목을 선택하세요.", HelpBoxMessageType.None));
                return;
            }

            ScrollView detail = InventoryEditorUIFactory.BeginDetailPanel(detailHost) as ScrollView;
            detail.Add(InventoryInspectorUI.BuildHeader(entry.name));

            detail.Add(CreateField("Name (C# identifier)", entry.name, value =>
            {
                entry.name = value;
                entry.displayName = value;
                SaveAndRefresh();
            }, IsProtectedEntry(entry)));

            detail.Add(CreateIntField("Value", entry.value, value =>
            {
                entry.value = value;
                SaveAndRefresh();
            }, IsProtectedEntry(entry)));

            detail.Add(CreateTextArea("Description", entry.description, value =>
            {
                entry.description = value;
                SaveAndRefresh();
            }));
        }

        private static VisualElement CreateField(string label, string value, Action<string> onChanged, bool readOnly = false)
        {
            var field = new TextField(label) { value = value ?? string.Empty, isReadOnly = readOnly };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            field.style.marginBottom = 4;
            return field;
        }

        private static VisualElement CreateIntField(string label, int value, Action<int> onChanged, bool readOnly = false)
        {
            var field = new IntegerField(label) { value = value, isReadOnly = readOnly };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            field.style.marginBottom = 4;
            return field;
        }

        private static VisualElement CreateTextArea(string label, string value, Action<string> onChanged)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                multiline = true
            };
            field.style.height = 72;
            field.style.marginBottom = 4;
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            return field;
        }

        private bool IsProtectedEntry(InventoryEnumEntryData entry) => entry.value == 0;

        private void AddEntry()
        {
            InventoryEnumEntryData[] entries = GetActiveEntries();
            int nextValue = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].value >= nextValue)
                    nextValue = entries[i].value + 1;
            }

            string baseName = category == EnumCategory.ItemType ? "CustomType" : "CustomKind";
            string uniqueName = baseName;
            int suffix = 1;
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null)
                    names.Add(entries[i].name);
            }

            while (names.Contains(uniqueName))
                uniqueName = baseName + suffix++;

            var list = new List<InventoryEnumEntryData>(entries)
            {
                new InventoryEnumEntryData
                {
                    name = uniqueName,
                    value = nextValue,
                    displayName = uniqueName,
                    description = string.Empty
                }
            };

            SetActiveEntries(list.ToArray());
            selectedIndex = list.Count - 1;
            SaveDocument();
            RebuildList();
            RebuildDetail();
            UpdateStatus();
        }

        private void RemoveSelected()
        {
            if (!IsValidSelection())
                return;

            InventoryEnumEntryData entry = GetSelected();
            if (IsProtectedEntry(entry))
            {
                EditorUtility.DisplayDialog("Remove", "value=0 항목은 삭제할 수 없습니다.", "OK");
                return;
            }

            var list = new List<InventoryEnumEntryData>(GetActiveEntries());
            list.RemoveAt(selectedIndex);
            SetActiveEntries(list.ToArray());
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, list.Count - 1);
            SaveDocument();
            RebuildList();
            RebuildDetail();
            UpdateStatus();
        }

        private void MoveSelected(int delta)
        {
            if (!IsValidSelection())
                return;

            InventoryEnumEntryData[] entries = GetActiveEntries();
            int target = selectedIndex + delta;
            if (target < 0 || target >= entries.Length)
                return;

            if (IsProtectedEntry(entries[selectedIndex]))
                return;

            if (target == 0 && IsProtectedEntry(entries[0]))
                return;

            var list = new List<InventoryEnumEntryData>(entries);
            InventoryEnumEntryData moving = list[selectedIndex];
            list.RemoveAt(selectedIndex);
            list.Insert(target, moving);

            InventoryEnumEntryData[] reordered = list.ToArray();
            InventoryEnumSettingsStore.RenumberValuesByOrder(reordered, out Dictionary<int, int> oldToNew);
            SetActiveEntries(reordered);
            InventoryEnumSettingsStore.RemapRouteValues(
                document,
                remapItemTypes: category == EnumCategory.ItemType,
                remapContainerKinds: category == EnumCategory.ContainerKind,
                oldToNew);
            selectedIndex = target;
            SaveDocument();
            RebuildList();
            RebuildDetail();
        }

        private void ResetDefaults()
        {
            if (!EditorUtility.DisplayDialog(
                    "Reset Defaults",
                    "ItemType / ContainerKind 설정을 기본값으로 되돌립니다. 계속할까요?",
                    "Reset",
                    "Cancel"))
                return;

            document = InventoryEnumSettingsStore.CreateDefaultDocument();
            selectedIndex = -1;
            SaveDocument();
            RebuildList();
            RebuildDetail();
            UpdateStatus();
        }

        private void SaveDocument()
        {
            InventoryEnumSettingsStore.Save(document);
            UpdateStatus();
        }

        private void SaveAndRefresh()
        {
            SaveDocument();
            RebuildList();
            UpdateStatus();
        }

        private void GenerateEnums()
        {
            if (!InventoryEnumSettingsStore.TryValidate(document, out string error))
            {
                EditorUtility.DisplayDialog("Generate Enums", error, "OK");
                UpdateStatus(error);
                return;
            }

            InventoryEnumSettingsStore.Save(document);
            bool changed = InventoryEnumScriptGenerator.Generate(document);
            UpdateStatus(changed ? "Enum scripts generated." : "Enum scripts are already up to date.");
            AssetDatabase.Refresh();
        }

        private void UpdateStatus(string message = null)
        {
            if (statusLabel == null)
                return;

            statusLabel.text = message ?? $"Settings: {InventoryEnumSettingsStore.RelativeFilePath}";
        }
    }
}
