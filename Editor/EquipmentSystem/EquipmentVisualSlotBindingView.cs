using System;
using System.Collections.Generic;
using System.Linq;
using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.EquipmentSystem
{
    internal sealed class EquipmentVisualSlotBindingView
    {
        private const string DragDisplayIndexKey = "PJDev.EquipmentVisualSlotDisplayIndex";
        private const string NoneLabel = "(None)";
        private const string MissingSuffix = " (missing)";
        private const int DragHandleWidth = 16;
        private const float ListScrollHeight = 280f;

        private readonly SerializedObject serializedObject;
        private readonly Action onChanged;

        private VisualElement rowsHost;
        private ScrollView scrollView;
        private HelpBox duplicateWarning;

        public EquipmentVisualSlotBindingView(SerializedObject serializedObject, Action onChanged)
        {
            this.serializedObject = serializedObject;
            this.onChanged = onChanged;
        }

        public VisualElement BuildRoot()
        {
            var section = InventoryEditorUIFactory.CreateSection("Slot Socket Bindings");
            section.style.marginTop = 8;

            section.Add(new Label(
                "한 줄 = 장착 슬롯 하나. ⋮⋮ 핸들로만 순서를 바꿀 수 있으며, EquipSlotIndex(0,1,2…)는 자동으로 매겨집니다.")
            {
                style = { opacity = 0.78f, fontSize = 11, marginBottom = 8, whiteSpace = WhiteSpace.Normal }
            });

            duplicateWarning = new HelpBox(
                "동일한 EquipSlotIndex가 여러 행에 있습니다. ⋮⋮ 드래그로 순서를 정리하세요.",
                HelpBoxMessageType.Warning);
            duplicateWarning.style.display = DisplayStyle.None;
            duplicateWarning.style.marginBottom = 6;
            section.Add(duplicateWarning);

            section.Add(BuildHeader());

            scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    height = ListScrollHeight,
                    minHeight = ListScrollHeight,
                    maxHeight = ListScrollHeight,
                    flexShrink = 0
                }
            };

            rowsHost = new VisualElement();
            scrollView.Add(rowsHost);
            section.Add(scrollView);

            return section;
        }

        public void Rebuild(IReadOnlyList<string> socketKeys, EquipmentSetupSO guide)
        {
            if (rowsHost == null)
                return;

            serializedObject.Update();
            var bindingsProp = serializedObject.FindProperty("slotSocketBindings");
            guide?.Normalize();

            Vector2 scrollOffset = scrollView?.scrollOffset ?? Vector2.zero;
            rowsHost.Clear();

            List<BindingEntry> bindings = ReadSortedBindings(bindingsProp);
            duplicateWarning.style.display = HasDuplicateSlotIndices(bindings)
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (bindings.Count == 0)
            {
                rowsHost.Add(new HelpBox(
                    "슬롯별 ObjectSocket 매핑을 추가하거나 Match Slot Count로 EquipmentSetupSO 슬롯 수에 맞추세요.",
                    HelpBoxMessageType.Info));
                return;
            }

            for (int displayIndex = 0; displayIndex < bindings.Count; displayIndex++)
                rowsHost.Add(BuildBindingRow(bindingsProp, bindings, displayIndex, guide, socketKeys));

            scrollView?.schedule.Execute(() =>
            {
                if (scrollView != null)
                    scrollView.scrollOffset = scrollOffset;
            });
        }

        public void MoveBinding(int fromDisplayIndex, int toDisplayIndex)
        {
            var bindingsProp = serializedObject.FindProperty("slotSocketBindings");
            List<BindingEntry> bindings = ReadSortedBindings(bindingsProp);

            if (fromDisplayIndex < 0 || fromDisplayIndex >= bindings.Count
                || toDisplayIndex < 0 || toDisplayIndex >= bindings.Count
                || fromDisplayIndex == toDisplayIndex)
                return;

            BindingEntry item = bindings[fromDisplayIndex];
            bindings.RemoveAt(fromDisplayIndex);
            bindings.Insert(toDisplayIndex, item);
            WriteBindings(bindingsProp, bindings);
            serializedObject.ApplyModifiedProperties();
            onChanged?.Invoke();
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingBottom = 4;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            header.style.marginBottom = 4;

            AddHeaderCell(header, "", DragHandleWidth + 4);
            AddHeaderCell(header, "Slot", 44);
            AddHeaderCell(header, "Category", 88);
            AddHeaderCell(header, "Socket", 160);
            AddHeaderCell(header, "", 28);

            return header;
        }

        private VisualElement BuildBindingRow(
            SerializedProperty bindingsProp,
            List<BindingEntry> sortedBindings,
            int displayIndex,
            EquipmentSetupSO guide,
            IReadOnlyList<string> socketKeys)
        {
            BindingEntry entry = sortedBindings[displayIndex];

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 32;
            row.style.marginBottom = 2;
            row.style.paddingLeft = 2;
            row.style.paddingRight = 2;
            row.AddToClassList(InventoryEditorStyles.EntryRowClass);

            RegisterRowDropTarget(row, displayIndex);
            row.Add(CreateDragHandle(displayIndex));

            row.Add(new Label(displayIndex.ToString())
            {
                style =
                {
                    width = 44,
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            });

            row.Add(new Label(GetCategoryLabel(guide, displayIndex))
            {
                style = { width = 88, fontSize = 11 }
            });

            var choices = BuildSocketChoices(socketKeys, entry.SocketKey);
            var popup = new PopupField<string>(
                choices,
                ResolvePopupIndex(choices, entry.SocketKey))
            {
                style = { width = 160 }
            };
            popup.RegisterValueChangedCallback(evt =>
            {
                bindingsProp.GetArrayElementAtIndex(entry.SourceArrayIndex)
                    .FindPropertyRelative("SocketKey").stringValue = ResolveSocketKey(evt.newValue);
                serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            });
            row.Add(popup);

            var removeButton = InventoryEditorUIFactory.CreateToolbarButton("×", () =>
            {
                bindingsProp.DeleteArrayElementAtIndex(entry.SourceArrayIndex);
                serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            });
            removeButton.style.width = 28;
            removeButton.style.minWidth = 28;
            row.Add(removeButton);

            return row;
        }

        private VisualElement CreateDragHandle(int displayIndex)
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
                    marginRight = 4
                }
            };
            handle.AddToClassList("inv-btn");

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
                DragAndDrop.SetGenericData(DragDisplayIndexKey, displayIndex);
                DragAndDrop.StartDrag($"Slot {displayIndex}");
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

        private void RegisterRowDropTarget(VisualElement row, int displayIndex)
        {
            row.RegisterCallback<DragEnterEvent>(evt =>
            {
                if (!TryGetDraggedDisplayIndex(out int fromIndex) || fromIndex == displayIndex)
                    return;

                SetRowDropHighlight(row, true);
                evt.StopPropagation();
            });

            row.RegisterCallback<DragLeaveEvent>(_ => SetRowDropHighlight(row, false));

            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!TryGetDraggedDisplayIndex(out int fromIndex) || fromIndex == displayIndex)
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.StopPropagation();
            });

            row.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!TryGetDraggedDisplayIndex(out int fromIndex) || fromIndex == displayIndex)
                    return;

                DragAndDrop.AcceptDrag();
                MoveBinding(fromIndex, displayIndex);
                SetRowDropHighlight(row, false);
                evt.StopPropagation();
            });
        }

        private static bool TryGetDraggedDisplayIndex(out int displayIndex)
        {
            if (DragAndDrop.GetGenericData(DragDisplayIndexKey) is int index)
            {
                displayIndex = index;
                return true;
            }

            displayIndex = -1;
            return false;
        }

        private static void SetRowDropHighlight(VisualElement row, bool enabled)
        {
            row.style.backgroundColor = enabled
                ? new Color(0.25f, 0.45f, 0.85f, 0.25f)
                : StyleKeyword.Null;
        }

        private static List<BindingEntry> ReadSortedBindings(SerializedProperty bindingsProp)
        {
            var list = new List<BindingEntry>(bindingsProp.arraySize);

            for (int i = 0; i < bindingsProp.arraySize; i++)
            {
                SerializedProperty element = bindingsProp.GetArrayElementAtIndex(i);
                list.Add(new BindingEntry
                {
                    SourceArrayIndex = i,
                    EquipSlotIndex = element.FindPropertyRelative("EquipSlotIndex").intValue,
                    SocketKey = element.FindPropertyRelative("SocketKey").stringValue ?? string.Empty
                });
            }

            list.Sort((a, b) => a.EquipSlotIndex.CompareTo(b.EquipSlotIndex));
            return list;
        }

        private static void WriteBindings(SerializedProperty bindingsProp, IReadOnlyList<BindingEntry> sortedBindings)
        {
            bindingsProp.arraySize = sortedBindings.Count;

            for (int i = 0; i < sortedBindings.Count; i++)
            {
                SerializedProperty element = bindingsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EquipSlotIndex").intValue = i;
                element.FindPropertyRelative("SocketKey").stringValue = sortedBindings[i].SocketKey ?? string.Empty;
            }
        }

        private static bool HasDuplicateSlotIndices(IReadOnlyList<BindingEntry> bindings)
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < bindings.Count; i++)
            {
                if (!seen.Add(bindings[i].EquipSlotIndex))
                    return true;
            }

            return false;
        }

        private static void AddHeaderCell(VisualElement row, string text, float width)
        {
            row.Add(new Label(text)
            {
                style =
                {
                    width = width,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11
                }
            });
        }

        private static string GetCategoryLabel(EquipmentSetupSO guide, int slotIndex)
        {
            if (guide == null)
                return "—";

            guide.Normalize();
            if (slotIndex < 0 || slotIndex >= guide.SlotCategories.Length)
                return "—";

            return guide.SlotCategories[slotIndex];
        }

        private static List<string> BuildSocketChoices(IReadOnlyList<string> discovered, string currentKey)
        {
            var choices = new List<string> { NoneLabel };

            if (!string.IsNullOrEmpty(currentKey) && !discovered.Any(key => key == currentKey))
                choices.Add(currentKey + MissingSuffix);

            choices.AddRange(discovered);
            return choices;
        }

        private static int ResolvePopupIndex(IReadOnlyList<string> choices, string currentKey)
        {
            if (string.IsNullOrEmpty(currentKey))
                return 0;

            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i] == currentKey || choices[i] == currentKey + MissingSuffix)
                    return i;
            }

            return 0;
        }

        private static string ResolveSocketKey(string popupLabel)
        {
            if (popupLabel == NoneLabel)
                return string.Empty;

            if (popupLabel.EndsWith(MissingSuffix, StringComparison.Ordinal))
                return popupLabel.Substring(0, popupLabel.Length - MissingSuffix.Length);

            return popupLabel;
        }

        private sealed class DragState
        {
            public Vector2 Start;
            public bool Dragging;
        }

        private sealed class BindingEntry
        {
            public int SourceArrayIndex;
            public int EquipSlotIndex;
            public string SocketKey;
        }
    }
}
