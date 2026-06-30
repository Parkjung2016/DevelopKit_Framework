using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.EquipmentSystem
{
    internal sealed class EquipmentSlotLayoutView
    {
        private const float SlotScrollHeight = 280f;
        private const int CategoryControlWidth = 272;

        private readonly Action<int, string> onCategoryChanged;
        private readonly Action onSyncRequested;
        private readonly List<SlotRowEntry> rows = new();

        private VisualElement rowsHost;
        private ScrollView scrollView;
        private string tagPrefix = "equip.";
        private bool isSyncing;

        public EquipmentSlotLayoutView(Action<int, string> onCategoryChanged, Action onSyncRequested)
        {
            this.onCategoryChanged = onCategoryChanged;
            this.onSyncRequested = onSyncRequested;
        }

        public void Mount(VisualElement host)
        {
            host.Clear();

            var section = InventoryEditorUIFactory.CreateSection("Slot Layout");
            section.Add(new Label("한 줄이 장비 슬롯 하나입니다. 왼쪽 숫자(0, 1, 2…)가 코드·UI에서 쓰는 equipSlotIndex입니다.")
            {
                style = { opacity = 0.78f, fontSize = 11, marginBottom = 8, whiteSpace = WhiteSpace.Normal }
            });

            scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "equip-slot-scroll",
                style =
                {
                    height = SlotScrollHeight,
                    minHeight = SlotScrollHeight,
                    maxHeight = SlotScrollHeight,
                    flexShrink = 0
                }
            };

            rowsHost = new VisualElement { name = "equip-slot-rows" };
            scrollView.Add(rowsHost);
            section.Add(scrollView);

            var syncRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            syncRow.Add(new Button(onSyncRequested)
            {
                text = "슬롯 배열 동기화",
                tooltip = "Slot Count와 SlotCategories 길이가 맞지 않을 때 누르세요."
            });
            section.Add(syncRow);

            host.Add(section);
        }

        public void Sync(EquipmentSetupSO setup)
        {
            if (setup == null || rowsHost == null || isSyncing)
                return;

            isSyncing = true;
            try
            {
                Vector2 scrollOffset = scrollView.scrollOffset;
                setup.Normalize();
                tagPrefix = ResolveTagPrefix(setup);

                while (rows.Count < setup.SlotCount)
                    AddRow(rows.Count, setup);

                while (rows.Count > setup.SlotCount)
                    RemoveLastRow();

                for (int i = 0; i < rows.Count; i++)
                {
                    string category = GetCategoryAt(setup, i);
                    SlotRowEntry row = rows[i];
                    row.SlotLabel.text = $"Slot {i}";

                    bool categoryChanged = row.CurrentCategory != category;
                    row.CurrentCategory = category;
                    row.UpdateTagHint(tagPrefix, category);

                    if (categoryChanged)
                        row.RefreshCategoryControl(tagPrefix, onCategoryChanged);
                }

                scrollView.schedule.Execute(() => scrollView.scrollOffset = scrollOffset);
            }
            finally
            {
                isSyncing = false;
            }
        }

        public void UpdateTagHints(EquipmentSetupSO setup)
        {
            if (setup == null)
                return;

            tagPrefix = ResolveTagPrefix(setup);
            for (int i = 0; i < rows.Count; i++)
                rows[i].UpdateTagHint(tagPrefix, rows[i].CurrentCategory);
        }

        private void AddRow(int slotIndex, EquipmentSetupSO setup)
        {
            string category = GetCategoryAt(setup, slotIndex);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 40;
            row.style.height = 40;
            row.style.marginBottom = 4;
            row.AddToClassList(InventoryEditorStyles.EntryRowClass);

            var slotLabel = new Label($"Slot {slotIndex}")
            {
                style = { width = 56, flexShrink = 0, unityFontStyleAndWeight = FontStyle.Bold }
            };
            row.Add(slotLabel);

            var categoryHost = CreateCategoryHost();
            row.Add(categoryHost);

            var tagLabel = new Label(EquipmentEditorUI.FormatTagHint(tagPrefix, category))
            {
                name = "equip-slot-tag-hint",
                style = { flexGrow = 1, flexShrink = 1, minWidth = 0, marginLeft = 8, fontSize = 11, opacity = 0.72f }
            };
            tagLabel.AddToClassList(InventoryEditorStyles.TextEllipsisClass);
            row.Add(tagLabel);

            var entry = new SlotRowEntry(slotIndex, row, slotLabel, categoryHost, tagLabel, category);
            entry.RefreshCategoryControl(tagPrefix, onCategoryChanged);
            rows.Add(entry);
            rowsHost.Add(row);
        }

        private void RemoveLastRow()
        {
            if (rows.Count == 0)
                return;

            int lastIndex = rows.Count - 1;
            rowsHost.Remove(rows[lastIndex].Row);
            rows.RemoveAt(lastIndex);
        }

        private static VisualElement CreateCategoryHost()
        {
            return new VisualElement
            {
                style =
                {
                    width = CategoryControlWidth,
                    minWidth = CategoryControlWidth,
                    maxWidth = CategoryControlWidth,
                    minHeight = 24,
                    maxHeight = 24,
                    flexShrink = 0
                }
            };
        }

        private static string GetCategoryAt(EquipmentSetupSO setup, int index)
        {
            if (setup.SlotCategories == null || index >= setup.SlotCategories.Length)
                return string.Empty;

            return setup.SlotCategories[index] ?? string.Empty;
        }

        private static string ResolveTagPrefix(EquipmentSetupSO setup) =>
            string.IsNullOrEmpty(setup.EquipmentTagPrefix) ? "equip." : setup.EquipmentTagPrefix;

        private sealed class SlotRowEntry
        {
            public int SlotIndex { get; }
            public VisualElement Row { get; }
            public Label SlotLabel { get; }
            public VisualElement CategoryHost { get; }
            public Label TagLabel { get; }
            public string CurrentCategory { get; set; }

            public SlotRowEntry(
                int slotIndex,
                VisualElement row,
                Label slotLabel,
                VisualElement categoryHost,
                Label tagLabel,
                string currentCategory)
            {
                SlotIndex = slotIndex;
                Row = row;
                SlotLabel = slotLabel;
                CategoryHost = categoryHost;
                TagLabel = tagLabel;
                CurrentCategory = currentCategory;
            }

            public void UpdateTagHint(string prefix, string category) =>
                TagLabel.text = EquipmentEditorUI.FormatTagHint(prefix, category);

            public void RefreshCategoryControl(string prefix, Action<int, string> onCategoryChanged)
            {
                void UpdateTag(string category)
                {
                    CurrentCategory = category;
                    UpdateTagHint(prefix, category);
                }

                if (EquipmentEditorUI.ShouldUseCustomCategoryField(CurrentCategory))
                    EquipmentEditorUI.BuildCustomCategoryControl(CategoryHost, CurrentCategory, SlotIndex, UpdateTag, onCategoryChanged);
                else
                    EquipmentEditorUI.BuildPresetCategoryControl(CategoryHost, CurrentCategory, SlotIndex, UpdateTag, onCategoryChanged);
            }
        }
    }
}
