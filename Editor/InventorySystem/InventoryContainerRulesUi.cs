using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal sealed class ContainerRulesUIBinding
    {
        public Action RefreshCapacitySection;
    }

    internal static class InventoryContainerRulesUI
    {
        public static string DescribeSlotRule(InventoryConfigSO config)
        {
            if (config == null)
                return "-";

            return config.SlotRuleMode == InventorySlotRuleMode.ItemType
                ? FormatItemTypes(config.AllowedSlotTypes)
                : "모든 아이템";
        }

        public static string DescribeCapacityRule(InventoryConfigSO config)
        {
            if (config == null)
                return "-";

            return config.CapacityRuleMode switch
            {
                InventoryCapacityRuleMode.Weight => $"무게 ≤ {config.MaxWeight:0.##}",
                InventoryCapacityRuleMode.SlotCount => $"슬롯 ≤ {config.MaxOccupiedSlots}",
                _ => "제한 없음"
            };
        }

        public static VisualElement Build(
            InventoryConfigSO config,
            SerializedObject serializedObject,
            Action onChanged,
            Action onRebuildDetail = null,
            ContainerRulesUIBinding binding = null)
        {
            var root = new VisualElement();
            serializedObject.Update();
            binding ??= new ContainerRulesUIBinding();

            var capacityHost = new VisualElement { name = "inv-capacity-section-host" };

            void RefreshCapacitySection()
            {
                capacityHost.Clear();
                capacityHost.Add(BuildCapacityRuleSection(config, NotifyChanged, onRebuildDetail));
            }

            binding.RefreshCapacitySection = RefreshCapacitySection;

            void NotifyChanged()
            {
                config.NormalizeCapacityLimits();
                serializedObject.Update();
                EditorUtility.SetDirty(config);
                binding.RefreshCapacitySection?.Invoke();
                onChanged?.Invoke();
            }

            var basics = InventoryEditorUIFactory.CreateSection("기본 설정");
            InventoryEditorUIFactory.BindPropertyFields(
                basics,
                serializedObject,
                NotifyChanged,
                "ContainerId",
                "Kind",
                "SlotCount");
            root.Add(basics);

            root.Add(BuildSlotRuleSection(config, NotifyChanged, onRebuildDetail));
            root.Add(capacityHost);
            binding.RefreshCapacitySection();

            root.userData = binding;
            return root;
        }

        private static VisualElement BuildSlotRuleSection(
            InventoryConfigSO config,
            Action onChanged,
            Action onRebuildDetail)
        {
            var section = InventoryEditorUIFactory.CreateSection("슬롯 규칙 (Slot Rule)");
            section.Add(new Label(DescribeSlotRule(config))
            {
                tooltip = "이 컨테이너 슬롯에 들어갈 수 있는 아이템 종류",
                style = { opacity = 0.78f, fontSize = 11, marginBottom = 6 }
            });

            var modeRow = new VisualElement();
            modeRow.AddToClassList(InventoryEditorStyles.RuleModeRowClass);
            modeRow.Add(CreateModeButton(
                "모든 아이템",
                config.SlotRuleMode == InventorySlotRuleMode.Any,
                () => SetSlotRuleMode(config, InventorySlotRuleMode.Any, onChanged, onRebuildDetail)));
            modeRow.Add(CreateModeButton(
                "타입 제한",
                config.SlotRuleMode == InventorySlotRuleMode.ItemType,
                () => SetSlotRuleMode(config, InventorySlotRuleMode.ItemType, onChanged, onRebuildDetail)));
            section.Add(modeRow);

            if (config.SlotRuleMode == InventorySlotRuleMode.Any)
            {
                section.Add(new HelpBox(
                    "모든 아이템 타입을 슬롯에 넣을 수 있습니다.",
                    HelpBoxMessageType.None));
            }
            else
            {
                section.Add(BuildItemTypeChipGrid(config, onChanged));
                if (config.AllowedSlotTypes == null || config.AllowedSlotTypes.Length == 0)
                {
                    section.Add(new HelpBox(
                        "허용 타입이 없으면 어떤 아이템도 넣을 수 없습니다. 칩을 선택하세요.",
                        HelpBoxMessageType.Warning));
                }
            }

            return section;
        }

        private static VisualElement BuildCapacityRuleSection(
            InventoryConfigSO config,
            Action onChanged,
            Action onRebuildDetail)
        {
            var section = InventoryEditorUIFactory.CreateSection("용량 규칙 (Capacity Rule)");
            section.Add(new Label(DescribeCapacityRule(config))
            {
                tooltip = "추가 가능 여부를 제한하는 규칙 (무게 또는 점유 슬롯 수)",
                style = { opacity = 0.78f, fontSize = 11, marginBottom = 6 }
            });

            var modeRow = new VisualElement();
            modeRow.AddToClassList(InventoryEditorStyles.RuleModeRowClass);
            modeRow.Add(CreateModeButton(
                "제한 없음",
                config.CapacityRuleMode == InventoryCapacityRuleMode.None,
                () => SetCapacityRuleMode(config, InventoryCapacityRuleMode.None, onChanged, onRebuildDetail)));
            modeRow.Add(CreateModeButton(
                "무게 제한",
                config.CapacityRuleMode == InventoryCapacityRuleMode.Weight,
                () => SetCapacityRuleMode(config, InventoryCapacityRuleMode.Weight, onChanged, onRebuildDetail)));
            modeRow.Add(CreateModeButton(
                "슬롯 수 제한",
                config.CapacityRuleMode == InventoryCapacityRuleMode.SlotCount,
                () => SetCapacityRuleMode(config, InventoryCapacityRuleMode.SlotCount, onChanged, onRebuildDetail)));
            section.Add(modeRow);

            switch (config.CapacityRuleMode)
            {
                case InventoryCapacityRuleMode.None:
                    section.Add(new HelpBox(
                        "무게·슬롯 수 제한 없이 슬롯 개수(Slot Count)만 적용됩니다.",
                        HelpBoxMessageType.None));
                    break;

                case InventoryCapacityRuleMode.Weight:
                    section.Add(BuildFloatStepper(
                        "최대 무게",
                        config.MaxWeight,
                        1f,
                        value =>
                        {
                            Undo.RecordObject(config, "Edit Max Weight");
                            config.MaxWeight = value;
                            EditorUtility.SetDirty(config);
                            onChanged?.Invoke();
                        }));
                    section.Add(new HelpBox(
                        "아이템 Weight × 수량의 합이 최대 무게를 넘으면 추가가 거부됩니다.",
                        HelpBoxMessageType.None));
                    break;

                case InventoryCapacityRuleMode.SlotCount:
                    int maxOccupiedLimit = Mathf.Max(1, config.SlotCount);
                    section.Add(BuildIntStepper(
                        "최대 점유 슬롯",
                        Mathf.Min(config.MaxOccupiedSlots, maxOccupiedLimit),
                        1,
                        maxOccupiedLimit,
                        value =>
                        {
                            Undo.RecordObject(config, "Edit Max Occupied Slots");
                            config.MaxOccupiedSlots = value;
                            config.NormalizeCapacityLimits();
                            EditorUtility.SetDirty(config);
                            onChanged?.Invoke();
                        }));
                    section.Add(new HelpBox(
                        $"점유 슬롯 상한은 Slot Count({config.SlotCount})를 넘을 수 없습니다.",
                        HelpBoxMessageType.None));
                    break;
            }

            return section;
        }

        private static VisualElement BuildItemTypeChipGrid(InventoryConfigSO config, Action onChanged)
        {
            var grid = new VisualElement();
            grid.AddToClassList(InventoryEditorStyles.TypeChipGridClass);

            var allowed = new HashSet<ItemType>(config.AllowedSlotTypes ?? Array.Empty<ItemType>());
            Array itemTypes = Enum.GetValues(typeof(ItemType));

            for (int i = 0; i < itemTypes.Length; i++)
            {
                var itemType = (ItemType)itemTypes.GetValue(i);
                if (itemType == ItemType.None)
                    continue;

                bool selected = allowed.Contains(itemType);
                Button chip = null;
                chip = new Button(() =>
                {
                    ToggleAllowedType(config, itemType, onChanged);
                    bool nowSelected = Array.IndexOf(config.AllowedSlotTypes ?? Array.Empty<ItemType>(), itemType) >= 0;
                    chip.EnableInClassList(InventoryEditorStyles.TypeChipSelectedClass, nowSelected);
                })
                {
                    text = itemType.ToString(),
                    tooltip = selected ? $"{itemType} 허용 (클릭하여 제외)" : $"{itemType} 제외됨 (클릭하여 허용)"
                };
                chip.AddToClassList(InventoryEditorStyles.TypeChipClass);
                if (selected)
                    chip.AddToClassList(InventoryEditorStyles.TypeChipSelectedClass);

                grid.Add(chip);
            }

            return grid;
        }

        private static void ToggleAllowedType(InventoryConfigSO config, ItemType itemType, Action onChanged)
        {
            var allowed = new HashSet<ItemType>(config.AllowedSlotTypes ?? Array.Empty<ItemType>());
            if (allowed.Contains(itemType))
            {
                if (allowed.Count <= 1)
                    return;

                allowed.Remove(itemType);
            }
            else
            {
                allowed.Add(itemType);
            }

            Undo.RecordObject(config, "Edit Allowed Item Types");
            config.AllowedSlotTypes = ToSortedArray(allowed);
            EditorUtility.SetDirty(config);
            onChanged?.Invoke();
        }

        private static VisualElement BuildIntStepper(
            string label,
            int value,
            int minValue,
            int maxValue,
            Action<int> onCommit)
        {
            var panel = new VisualElement();
            panel.AddToClassList(InventoryEditorStyles.RuleStepperClass);
            panel.Add(new Label(label) { style = { fontSize = 11, marginBottom = 4 } });

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var field = new IntegerField { value = value, style = { width = 72 } };
            void Commit(int next)
            {
                next = Mathf.Clamp(next, minValue, maxValue);
                field.SetValueWithoutNotify(next);
                onCommit?.Invoke(next);
            }

            var minus = InventoryEditorUIFactory.CreateToolbarButton("-", () => Commit(field.value - 1));
            var plus = InventoryEditorUIFactory.CreateToolbarButton("+", () => Commit(field.value + 1));
            field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(field.value);
                field.Blur();
                evt.StopPropagation();
            });

            row.Add(minus);
            row.Add(field);
            row.Add(plus);
            panel.Add(row);
            return panel;
        }

        private static VisualElement BuildFloatStepper(string label, float value, float step, Action<float> onCommit)
        {
            var panel = new VisualElement();
            panel.AddToClassList(InventoryEditorStyles.RuleStepperClass);
            panel.Add(new Label(label) { style = { fontSize = 11, marginBottom = 4 } });

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var field = new FloatField { value = value, style = { width = 72 } };
            void Commit(float next)
            {
                next = Mathf.Max(0f, next);
                field.SetValueWithoutNotify(next);
                onCommit?.Invoke(next);
            }

            var minus = InventoryEditorUIFactory.CreateToolbarButton("-", () => Commit(field.value - step));
            var plus = InventoryEditorUIFactory.CreateToolbarButton("+", () => Commit(field.value + step));
            field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(field.value);
                field.Blur();
                evt.StopPropagation();
            });

            row.Add(minus);
            row.Add(field);
            row.Add(plus);
            panel.Add(row);
            return panel;
        }

        private static Button CreateModeButton(string label, bool active, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.AddToClassList(InventoryEditorStyles.RuleModeButtonClass);
            if (active)
                button.AddToClassList(InventoryEditorStyles.RuleModeButtonActiveClass);
            return button;
        }

        private static void SetSlotRuleMode(
            InventoryConfigSO config,
            InventorySlotRuleMode mode,
            Action onChanged,
            Action onRebuildDetail)
        {
            Undo.RecordObject(config, "Change Slot Rule Mode");
            config.SlotRuleMode = mode;

            if (mode == InventorySlotRuleMode.ItemType &&
                (config.AllowedSlotTypes == null || config.AllowedSlotTypes.Length == 0))
            {
                config.AllowedSlotTypes = DefaultAllowedTypesForKind(config.Kind);
            }

            EditorUtility.SetDirty(config);
            onChanged?.Invoke();
            onRebuildDetail?.Invoke();
        }

        private static void SetCapacityRuleMode(
            InventoryConfigSO config,
            InventoryCapacityRuleMode mode,
            Action onChanged,
            Action onRebuildDetail)
        {
            Undo.RecordObject(config, "Change Capacity Rule Mode");
            config.CapacityRuleMode = mode;

            if (mode == InventoryCapacityRuleMode.SlotCount)
            {
                if (config.MaxOccupiedSlots <= 0)
                    config.MaxOccupiedSlots = Mathf.Max(1, config.SlotCount);
                config.NormalizeCapacityLimits();
            }

            if (mode == InventoryCapacityRuleMode.Weight && config.MaxWeight <= 0f)
                config.MaxWeight = 100f;

            EditorUtility.SetDirty(config);
            onChanged?.Invoke();
            onRebuildDetail?.Invoke();
        }

        private static ItemType[] DefaultAllowedTypesForKind(ContainerKind kind) =>
            kind == ContainerKind.Equipment
                ? new[] { ItemType.Equipment }
                : new[] { ItemType.General };

        private static ItemType[] ToSortedArray(HashSet<ItemType> allowed)
        {
            var list = new List<ItemType>(allowed);
            list.Sort((a, b) => ((int)a).CompareTo((int)b));
            return list.ToArray();
        }

        private static string FormatItemTypes(ItemType[] types)
        {
            if (types == null || types.Length == 0)
                return "타입 없음 (모두 거부)";

            var parts = new string[types.Length];
            for (int i = 0; i < types.Length; i++)
                parts[i] = types[i].ToString();

            return string.Join(", ", parts);
        }
    }
}
