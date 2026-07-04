using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.EquipmentSystem
{
    internal sealed class EquipmentSlotLayoutView
    {
        private const string DragSlotIndexKey = "PJDev.EquipmentSlotIndex";
        private const float SlotScrollHeight = 320f;
        private const int CategoryControlWidth = 272;
        private const int DragHandleWidth = 16;

        private readonly Action<int, string> onCategoryChanged;
        private readonly Action onSyncRequested;
        private readonly Action<int, int> onMoveSlot;
        private readonly List<SlotRowEntry> rows = new();

        private VisualElement rowsHost;
        private ScrollView scrollView;
        private string tagPrefix = "equip.";
        private bool isSyncing;

        public EquipmentSlotLayoutView(
            Action<int, string> onCategoryChanged,
            Action onSyncRequested,
            Action<int, int> onMoveSlot)
        {
            this.onCategoryChanged = onCategoryChanged;
            this.onSyncRequested = onSyncRequested;
            this.onMoveSlot = onMoveSlot;
        }

        public void Mount(VisualElement host)
        {
            host.Clear();

            var section = InventoryEditorUIFactory.CreateSection("Slot Layout");
            section.Add(new Label(
                "한 줄 = 장비 슬롯 하나. ⋮⋮ 핸들을 드래그해 순서를 바꿉니다. 숫자(0,1,2…)가 equipSlotIndex입니다.")
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
                    row.SlotIndex = i;
                    row.SlotLabel.text = $"Slot {i}";
                    SetRowDropHighlight(row, false);

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

            var entry = new SlotRowEntry(slotIndex, row, category);
            RegisterRowDropTarget(entry);

            row.Add(CreateDragHandle(entry));

            var slotLabel = new Label($"Slot {slotIndex}")
            {
                style = { width = 56, flexShrink = 0, unityFontStyleAndWeight = FontStyle.Bold }
            };
            entry.SlotLabel = slotLabel;
            row.Add(slotLabel);

            var categoryHost = CreateCategoryHost();
            entry.CategoryHost = categoryHost;
            row.Add(categoryHost);

            var tagLabel = new Label(EquipmentEditorUI.FormatTagHint(tagPrefix, category))
            {
                name = "equip-slot-tag-hint",
                style = { flexGrow = 1, flexShrink = 1, minWidth = 0, marginLeft = 8, fontSize = 11, opacity = 0.72f }
            };
            tagLabel.AddToClassList(InventoryEditorStyles.TextEllipsisClass);
            entry.TagLabel = tagLabel;
            row.Add(tagLabel);

            entry.RefreshCategoryControl(tagPrefix, onCategoryChanged);
            rows.Add(entry);
            rowsHost.Add(row);
        }

        private VisualElement CreateDragHandle(SlotRowEntry entry)
        {
            var handle = new Label("⋮⋮")
            {
                tooltip = "드래그해서 슬롯 순서 변경",
                style =
                {
                    width = DragHandleWidth,
                    minWidth = DragHandleWidth,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    opacity = 0.55f,
                    marginRight = 6,
                    cursor = new StyleCursor(StyleKeyword.Null)
                }
            };
            handle.AddToClassList("inv-btn");
            handle.style.cursor = StyleKeyword.Null;

            var dragState = new DragState();
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                dragState.Start = (Vector2)evt.position;
                dragState.Dragging = false;
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!handle.HasPointerCapture(evt.pointerId))
                    return;

                Vector2 delta = (Vector2)evt.position - dragState.Start;
                if (dragState.Dragging || delta.magnitude < 6f)
                    return;

                dragState.Dragging = true;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragSlotIndexKey, entry.SlotIndex);
                DragAndDrop.StartDrag($"Slot {entry.SlotIndex}");
                handle.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (handle.HasPointerCapture(evt.pointerId))
                    handle.ReleasePointer(evt.pointerId);
            });

            return handle;
        }

        private void RegisterRowDropTarget(SlotRowEntry entry)
        {
            VisualElement row = entry.Row;

            row.RegisterCallback<DragEnterEvent>(evt =>
            {
                if (!TryGetDraggedSlotIndex(out int fromIndex) || fromIndex == entry.SlotIndex)
                    return;

                SetRowDropHighlight(entry, true);
                evt.StopPropagation();
            });

            row.RegisterCallback<DragLeaveEvent>(evt =>
            {
                SetRowDropHighlight(entry, false);
            });

            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!TryGetDraggedSlotIndex(out int fromIndex) || fromIndex == entry.SlotIndex)
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.StopPropagation();
            });

            row.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!TryGetDraggedSlotIndex(out int fromIndex) || fromIndex == entry.SlotIndex)
                    return;

                DragAndDrop.AcceptDrag();
                onMoveSlot?.Invoke(fromIndex, entry.SlotIndex);
                SetRowDropHighlight(entry, false);
                evt.StopPropagation();
            });
        }

        private static bool TryGetDraggedSlotIndex(out int slotIndex)
        {
            if (DragAndDrop.GetGenericData(DragSlotIndexKey) is int index)
            {
                slotIndex = index;
                return true;
            }

            slotIndex = -1;
            return false;
        }

        private static void SetRowDropHighlight(SlotRowEntry entry, bool enabled)
        {
            entry.Row.style.backgroundColor = enabled
                ? new Color(0.25f, 0.45f, 0.85f, 0.25f)
                : StyleKeyword.Null;
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

        private sealed class DragState
        {
            public Vector2 Start;
            public bool Dragging;
        }

        private sealed class SlotRowEntry
        {
            public SlotRowEntry(int slotIndex, VisualElement row, string currentCategory)
            {
                SlotIndex = slotIndex;
                Row = row;
                CurrentCategory = currentCategory;
            }

            public int SlotIndex { get; set; }
            public VisualElement Row { get; }
            public Label SlotLabel { get; set; }
            public VisualElement CategoryHost { get; set; }
            public Label TagLabel { get; set; }
            public string CurrentCategory { get; set; }

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
