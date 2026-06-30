using System;
using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.EquipmentSystem
{
    internal sealed class EquipmentContainerSectionBinding
    {
        public VisualElement Root { get; private set; }
        public IntegerField SlotCountField { get; private set; }

        public static EquipmentContainerSectionBinding Build(
            EquipmentSetupSO setup,
            SerializedObject serializedObject,
            Action onSlotCountChanged,
            Action onMetaChanged,
            Action onItemTypeChanged)
        {
            var binding = new EquipmentContainerSectionBinding();
            binding.Root = binding.BuildRoot(setup, serializedObject, onSlotCountChanged, onMetaChanged, onItemTypeChanged);
            return binding;
        }

        public void UpdateSlotCountDisplay(int slotCount)
        {
            SlotCountField?.SetValueWithoutNotify(slotCount);
        }

        private VisualElement BuildRoot(
            EquipmentSetupSO setup,
            SerializedObject serializedObject,
            Action onSlotCountChanged,
            Action onMetaChanged,
            Action onItemTypeChanged)
        {
            var section = InventoryEditorUIFactory.CreateSection("Container");
            serializedObject.Update();

            InventoryEditorUIFactory.BindPropertyFields(
                section,
                serializedObject,
                onMetaChanged,
                "ContainerId",
                "Kind");

            section.Add(BuildSlotCountStepper(setup, serializedObject, onSlotCountChanged));
            section.Add(BuildEquipmentItemTypeSection(setup, serializedObject, onItemTypeChanged));

            return section;
        }

        private VisualElement BuildSlotCountStepper(
            EquipmentSetupSO setup,
            SerializedObject serializedObject,
            Action onSlotCountChanged)
        {
            const int minSlots = 1;
            const int maxSlots = 32;
            setup.Normalize();

            var panel = new VisualElement();
            panel.AddToClassList(InventoryEditorStyles.RuleStepperClass);
            panel.style.marginTop = 6;
            panel.style.marginBottom = 6;

            panel.Add(new Label("Slot Count")
            {
                tooltip = "Number of equipment slots",
                style = { fontSize = 11, marginBottom = 4 }
            });

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            SlotCountField = new IntegerField { value = setup.SlotCount, style = { width = 72 } };

            void Commit(int next)
            {
                next = Mathf.Clamp(next, minSlots, maxSlots);
                if (setup.SlotCount == next)
                {
                    SlotCountField.SetValueWithoutNotify(next);
                    return;
                }

                Undo.RecordObject(setup, "Change Equipment Slot Count");
                SerializedProperty slotCountProp = InventoryEditorUIFactory.FindSerializedProperty(serializedObject, "SlotCount");
                if (slotCountProp != null)
                    slotCountProp.intValue = next;
                else
                    setup.SlotCount = next;

                setup.Normalize();
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(setup);
                SlotCountField.SetValueWithoutNotify(setup.SlotCount);
                onSlotCountChanged?.Invoke();
            }

            var minus = InventoryEditorUIFactory.CreateToolbarButton("−", () => Commit(SlotCountField.value - 1));
            var plus = InventoryEditorUIFactory.CreateToolbarButton("+", () => Commit(SlotCountField.value + 1));
            SlotCountField.RegisterCallback<FocusOutEvent>(_ => Commit(SlotCountField.value));
            SlotCountField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(SlotCountField.value);
                SlotCountField.Blur();
                evt.StopPropagation();
            });

            row.Add(minus);
            row.Add(SlotCountField);
            row.Add(plus);
            panel.Add(row);
            panel.Add(new Label($"{minSlots}–{maxSlots} slots")
            {
                style = { fontSize = 10, opacity = 0.7f, marginTop = 4 }
            });

            return panel;
        }

        private static VisualElement BuildEquipmentItemTypeSection(
            EquipmentSetupSO setup,
            SerializedObject serializedObject,
            Action onItemTypeChanged)
        {
            var block = new VisualElement();
            block.style.marginTop = 4;

            block.Add(new Label("Equipment Item Type")
            {
                tooltip = "Only items of this ItemType can be placed in equipment slots.",
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginBottom = 4 }
            });
            block.Add(new Label("Usually Equipment. Pick from types defined in Inventory Enums.")
            {
                style = { opacity = 0.78f, fontSize = 11, marginBottom = 6, whiteSpace = WhiteSpace.Normal }
            });

            var grid = new VisualElement();
            grid.AddToClassList(InventoryEditorStyles.TypeChipGridClass);

            SerializedProperty itemTypeProp = InventoryEditorUIFactory.FindSerializedProperty(serializedObject, "EquipmentItemType");
            ItemType selected = setup.EquipmentItemType;

            foreach (ItemType itemType in InventoryEnumCatalog.GetSelectableItemTypes())
            {
                bool isSelected = selected.Equals(itemType);
                Button chip = null;
                chip = new Button(() =>
                {
                    if (setup.EquipmentItemType.Equals(itemType))
                        return;

                    Undo.RecordObject(setup, "Change Equipment Item Type");
                    if (itemTypeProp != null)
                    {
                        serializedObject.Update();
                        itemTypeProp.enumValueIndex = GetEnumValueIndex(itemTypeProp, itemType);
                        serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        setup.EquipmentItemType = itemType;
                    }

                    EditorUtility.SetDirty(setup);
                    serializedObject.Update();

                    foreach (var child in grid.Children())
                    {
                        if (child is Button childChip)
                            childChip.EnableInClassList(InventoryEditorStyles.TypeChipSelectedClass, false);
                    }

                    chip.EnableInClassList(InventoryEditorStyles.TypeChipSelectedClass, true);
                    onItemTypeChanged?.Invoke();
                })
                {
                    text = InventoryEnumCatalog.GetItemTypeDisplayName(itemType),
                    tooltip = isSelected
                        ? $"Selected: {InventoryEnumCatalog.GetItemTypeDisplayName(itemType)}"
                        : $"Set to {InventoryEnumCatalog.GetItemTypeDisplayName(itemType)}"
                };
                chip.AddToClassList(InventoryEditorStyles.TypeChipClass);
                if (isSelected)
                    chip.AddToClassList(InventoryEditorStyles.TypeChipSelectedClass);

                grid.Add(chip);
            }

            block.Add(grid);
            return block;
        }

        private static int GetEnumValueIndex(SerializedProperty enumProperty, ItemType itemType)
        {
            string enumName = Enum.GetName(typeof(ItemType), itemType);
            if (string.IsNullOrEmpty(enumName))
                return enumProperty.enumValueIndex;

            string[] names = enumProperty.enumNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == enumName)
                    return i;
            }

            return enumProperty.enumValueIndex;
        }
    }
}
