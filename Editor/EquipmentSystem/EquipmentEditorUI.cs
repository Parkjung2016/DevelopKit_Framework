using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.EquipmentSystem
{
    internal static class EquipmentEditorUI
    {
        internal const string ItemDatabasePrefsPrefix = "PJDev.EquipmentSetup.ItemDb.";

        internal static readonly (string Label, string Value)[] CategoryOptions =
        {
            ("Any", EquipmentSlotCategories.Any),
            ("Weapon", EquipmentSlotCategories.Weapon),
            ("Head", EquipmentSlotCategories.Head),
            ("Chest", EquipmentSlotCategories.Chest),
            ("Hands", EquipmentSlotCategories.Hands),
            ("Feet", EquipmentSlotCategories.Feet),
            ("Ring", EquipmentSlotCategories.Ring),
            ("OffHand", EquipmentSlotCategories.OffHand)
        };

        internal static readonly (string Name, string[] Categories)[] LayoutPresets =
        {
            ("기본 6슬롯", new[]
            {
                EquipmentSlotCategories.Weapon,
                EquipmentSlotCategories.Head,
                EquipmentSlotCategories.Chest,
                EquipmentSlotCategories.Hands,
                EquipmentSlotCategories.Feet,
                EquipmentSlotCategories.Ring
            }),
            ("RPG 8슬롯", new[]
            {
                EquipmentSlotCategories.Weapon,
                EquipmentSlotCategories.OffHand,
                EquipmentSlotCategories.Head,
                EquipmentSlotCategories.Chest,
                EquipmentSlotCategories.Hands,
                EquipmentSlotCategories.Feet,
                EquipmentSlotCategories.Ring,
                EquipmentSlotCategories.Any
            }),
            ("무기만", new[] { EquipmentSlotCategories.Weapon })
        };

        public static VisualElement BuildIntroHelpBox()
        {
            return new HelpBox(
                "장비 슬롯(무기·방어구) 컨테이너를 설정하는 에셋입니다.\n\n" +
                "1. Slot Layout — 슬롯마다 Weapon, Head 같은 카테고리 지정\n" +
                "2. Item Tag — 아이템에 equip.Weapon 태그 추가 (또는 Profile Overrides)\n" +
                "3. 런타임 — 아래 Integration Guide 참고해서 CreateContainer()로 등록",
                HelpBoxMessageType.Info);
        }

        public static VisualElement BuildPresetToolbar(EquipmentSetupSO setup, Action onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 6;

            var label = new Label("프리셋");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginRight = 8;
            label.style.alignSelf = Align.Center;
            row.Add(label);

            for (int i = 0; i < LayoutPresets.Length; i++)
            {
                (string name, string[] categories) = LayoutPresets[i];
                var button = new Button(() =>
                {
                    Undo.RecordObject(setup, "Apply Equipment Layout Preset");
                    setup.SlotCount = categories.Length;
                    setup.SlotCategories = (string[])categories.Clone();
                    setup.Normalize();
                    EditorUtility.SetDirty(setup);
                    onChanged?.Invoke();
                })
                {
                    text = name,
                    tooltip = $"{categories.Length}슬롯 · {string.Join(", ", categories)}"
                };
                button.AddToClassList("inv-btn");
                row.Add(button);
            }

            var resetButton = new Button(() =>
            {
                Undo.RecordObject(setup, "Reset Equipment Layout");
                setup.SlotCount = 6;
                setup.SlotCategories = new[]
                {
                    EquipmentSlotCategories.Weapon,
                    EquipmentSlotCategories.Head,
                    EquipmentSlotCategories.Chest,
                    EquipmentSlotCategories.Hands,
                    EquipmentSlotCategories.Feet,
                    EquipmentSlotCategories.Ring
                };
                setup.Normalize();
                EditorUtility.SetDirty(setup);
                onChanged?.Invoke();
            })
            {
                text = "기본값"
            };
            resetButton.AddToClassList("inv-btn");
            row.Add(resetButton);

            return row;
        }

        public static string FormatTagHint(string prefix, string category)
        {
            if (string.IsNullOrEmpty(category))
                return "→ 모든 아이템 허용";

            return $"→ 태그 {prefix}{category}";
        }

        internal static bool ShouldUseCustomCategoryField(string category)
        {
            if (string.IsNullOrEmpty(category))
                return false;

            return IndexOfCategoryValue(category) < 0;
        }

        internal static void BuildPresetCategoryControl(
            VisualElement host,
            string current,
            int slotIndex,
            Action<string> updateTagHint,
            Action<int, string> onCategoryChanged)
        {
            host.Clear();
            ConfigureCategoryHost(host);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.width = Length.Percent(100);

            var labels = GetCategoryLabels();
            int selectedIndex = IndexOfCategoryValue(current);
            int popupIndex = selectedIndex >= 0 ? selectedIndex : 0;

            var popup = new PopupField<string>(labels, popupIndex)
            {
                style = { flexGrow = 1, height = 24 }
            };
            popup.SetValueWithoutNotify(labels[popupIndex]);
            popup.RegisterValueChangedCallback(evt =>
            {
                string value = GetCategoryValue(evt.newValue);
                updateTagHint(value);
                onCategoryChanged?.Invoke(slotIndex, value);
            });

            var customButton = CreateCategoryIconButton(
                "✎",
                "직접 카테고리 입력",
                () =>
                {
                    BuildCustomCategoryControl(host, string.Empty, slotIndex, updateTagHint, onCategoryChanged);
                    updateTagHint(string.Empty);
                });
            customButton.style.marginLeft = 4;

            row.Add(popup);
            row.Add(customButton);
            host.Add(row);
        }

        internal static void BuildCustomCategoryControl(
            VisualElement host,
            string current,
            int slotIndex,
            Action<string> updateTagHint,
            Action<int, string> onCategoryChanged)
        {
            host.Clear();
            ConfigureCategoryHost(host);

            var shell = CreateCategoryInlineShell();

            var backButton = CreateCategoryIconButton(
                "‹",
                "프리셋 목록으로",
                () =>
                {
                    BuildPresetCategoryControl(host, EquipmentSlotCategories.Weapon, slotIndex, updateTagHint, onCategoryChanged);
                    updateTagHint(EquipmentSlotCategories.Weapon);
                    onCategoryChanged?.Invoke(slotIndex, EquipmentSlotCategories.Weapon);
                });

            var field = new TextField
            {
                value = current ?? string.Empty,
                style = { flexGrow = 1, minWidth = 0, marginTop = 0, marginBottom = 0 }
            };
            field.tooltip = "직접 입력 (예: Amulet, Belt)";

            field.RegisterValueChangedCallback(evt =>
            {
                string value = evt.newValue?.Trim() ?? string.Empty;
                updateTagHint(value);
                onCategoryChanged?.Invoke(slotIndex, value);
            });

            shell.Add(backButton);
            shell.Add(field);
            host.Add(shell);
        }

        private static void ConfigureCategoryHost(VisualElement host)
        {
            const int width = 272;
            host.style.width = width;
            host.style.minWidth = width;
            host.style.maxWidth = width;
            host.style.minHeight = 24;
            host.style.maxHeight = 24;
            host.style.flexShrink = 0;
        }

        private static VisualElement CreateCategoryInlineShell()
        {
            var shell = new VisualElement();
            shell.style.flexDirection = FlexDirection.Row;
            shell.style.alignItems = Align.Center;
            shell.style.height = 24;
            shell.style.flexGrow = 1;
            shell.style.borderTopWidth = shell.style.borderBottomWidth = 1;
            shell.style.borderLeftWidth = shell.style.borderRightWidth = 1;
            var borderColor = new Color(0.18f, 0.18f, 0.18f);
            shell.style.borderTopColor = borderColor;
            shell.style.borderBottomColor = borderColor;
            shell.style.borderLeftColor = borderColor;
            shell.style.borderRightColor = borderColor;
            shell.style.borderTopLeftRadius = shell.style.borderTopRightRadius = 3;
            shell.style.borderBottomLeftRadius = shell.style.borderBottomRightRadius = 3;
            shell.style.backgroundColor = new Color(0.26f, 0.26f, 0.26f);
            shell.style.paddingLeft = 4;
            shell.style.paddingRight = 6;
            return shell;
        }

        private static Button CreateCategoryIconButton(string text, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { text = text, tooltip = tooltip };
            button.style.width = 22;
            button.style.height = 22;
            button.style.minWidth = 22;
            button.style.minHeight = 22;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.fontSize = 11;
            button.AddToClassList("inv-btn");
            return button;
        }

        public static VisualElement BuildTagGuideSection(EquipmentSetupSO setup)
        {
            var section = InventoryEditorUIFactory.CreateSection("Item Tags");
            string prefix = string.IsNullOrEmpty(setup.EquipmentTagPrefix) ? "equip." : setup.EquipmentTagPrefix;

            section.Add(new HelpBox(
                "장착하려면 ItemDefinitionSO의 Tags에 아래 형식으로 넣어주세요.\n\n" +
                $"형식: {prefix}<카테고리>\n" +
                $"예) 검 → {prefix}{EquipmentSlotCategories.Weapon}, 투구 → {prefix}{EquipmentSlotCategories.Head}\n\n" +
                "태그 대신 아래 Profile Overrides에서 아이템별로 지정할 수도 있습니다.",
                HelpBoxMessageType.None));

            return section;
        }

        public static VisualElement BuildProfileOverridesSection(
            EquipmentSetupSO setup,
            SerializedObject serializedObject,
            ItemDatabaseSO itemDatabase,
            Action<ItemDatabaseSO> onItemDatabaseChanged,
            Action onChanged)
        {
            var section = InventoryEditorUIFactory.CreateSection("Profile Overrides (Optional)");

            section.Add(new Label("태그 없이 특정 아이템의 슬롯 카테고리를 지정할 때 씁니다.")
            {
                style = { opacity = 0.78f, fontSize = 11, marginBottom = 6, whiteSpace = WhiteSpace.Normal }
            });

            var dbRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
            dbRow.Add(new Label("Item Database") { style = { width = 110 } });
            var dbField = new ObjectField
            {
                objectType = typeof(ItemDatabaseSO),
                allowSceneObjects = false,
                value = itemDatabase,
                style = { flexGrow = 1 }
            };
            dbField.RegisterValueChangedCallback(evt =>
            {
                onItemDatabaseChanged?.Invoke(evt.newValue as ItemDatabaseSO);
            });
            dbRow.Add(dbField);
            section.Add(dbRow);

            if (itemDatabase == null)
            {
                section.Add(new HelpBox(
                    "같은 폴더의 Inventory Setup / Item Database를 자동으로 찾습니다. 없으면 위에서 직접 연결하세요.",
                    HelpBoxMessageType.Info));
            }

            SerializedProperty overridesProp = InventoryEditorUIFactory.FindSerializedProperty(
                serializedObject,
                "ItemProfileOverrides");
            if (overridesProp == null)
                return section;

            serializedObject.Update();
            var listHost = new VisualElement();
            section.Add(listHost);

            void RebuildList()
            {
                listHost.Clear();
                serializedObject.Update();
                overridesProp = InventoryEditorUIFactory.FindSerializedProperty(
                    serializedObject,
                    "ItemProfileOverrides");
                if (overridesProp == null)
                    return;

                for (int i = 0; i < overridesProp.arraySize; i++)
                {
                    SerializedProperty element = overridesProp.GetArrayElementAtIndex(i);
                    SerializedProperty itemIdProp = element.FindPropertyRelative("ItemId");
                    SerializedProperty categoryProp = element.FindPropertyRelative("SlotCategory");
                    int rowIndex = i;

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 4;
                    row.AddToClassList(InventoryEditorStyles.EntryRowClass);

                    ItemDefinitionSO currentItem = FindItemDefinition(itemDatabase, itemIdProp.intValue);
                    var itemField = new ObjectField
                    {
                        objectType = typeof(ItemDefinitionSO),
                        allowSceneObjects = false,
                        value = currentItem,
                        style = { flexGrow = 1, maxWidth = 200 }
                    };
                    itemField.RegisterValueChangedCallback(evt =>
                    {
                        var def = evt.newValue as ItemDefinitionSO;
                        int newItemId = def != null ? def.ItemId : 0;
                        if (itemIdProp.intValue == newItemId)
                            return;

                        serializedObject.Update();
                        itemIdProp.intValue = newItemId;
                        serializedObject.ApplyModifiedProperties();
                        onChanged?.Invoke();
                        RebuildList();
                    });
                    row.Add(itemField);

                    string categoryValue = categoryProp.stringValue ?? string.Empty;
                    int categoryIndex = IndexOfCategoryValue(categoryValue);
                    var labels = GetCategoryLabels();
                    int popupIndex = categoryIndex >= 0 ? categoryIndex : 0;
                    var categoryPopup = new PopupField<string>(labels, popupIndex)
                    {
                        style = { width = 180 }
                    };
                    categoryPopup.SetValueWithoutNotify(labels[popupIndex]);
                    categoryPopup.RegisterValueChangedCallback(evt =>
                    {
                        string newValue = GetCategoryValue(evt.newValue);
                        if ((categoryProp.stringValue ?? string.Empty) == newValue)
                            return;

                        serializedObject.Update();
                        categoryProp.stringValue = newValue;
                        serializedObject.ApplyModifiedProperties();
                        onChanged?.Invoke();
                    });
                    row.Add(categoryPopup);

                    var removeButton = new Button(() =>
                    {
                        serializedObject.Update();
                        overridesProp.DeleteArrayElementAtIndex(rowIndex);
                        serializedObject.ApplyModifiedProperties();
                        onChanged?.Invoke();
                        RebuildList();
                    })
                    {
                        text = "−"
                    };
                    removeButton.style.width = 28;
                    row.Add(removeButton);

                    listHost.Add(row);
                }

                var addButton = new Button(() =>
                {
                    serializedObject.Update();
                    overridesProp.arraySize++;
                    SerializedProperty element = overridesProp.GetArrayElementAtIndex(overridesProp.arraySize - 1);
                    element.FindPropertyRelative("ItemId").intValue = 0;
                    element.FindPropertyRelative("SlotCategory").stringValue = EquipmentSlotCategories.Weapon;
                    serializedObject.ApplyModifiedProperties();
                    onChanged?.Invoke();
                    RebuildList();
                })
                {
                    text = "+ Override 추가"
                };
                addButton.AddToClassList("inv-btn");
                listHost.Add(addButton);
            }

            RebuildList();
            return section;
        }

        public static VisualElement BuildIntegrationSection(EquipmentSetupSO setup)
        {
            var section = InventoryEditorUIFactory.CreateSection("Integration Guide");
            string assetName = setup.name;
            string snippet =
                $"// 장비 컨테이너 생성 (이 Setup의 슬롯 규칙 적용)\n" +
                $"var equipContainer = {assetName}.CreateContainer(itemDatabase);\n" +
                $"inventoryGroup.RegisterContainer(equipContainer);\n\n" +
                $"// EquipmentSystem 연결\n" +
                $"objectEquipment.Init(owner, inventorySystem, {assetName}, effectApplier);\n\n" +
                $"// 인벤 → 장비 슬롯 장착\n" +
                $"objectEquipment.TryEquipFromInventory(bagSlotIndex, equipSlotIndex);";

            section.Add(new HelpBox(
                "현재 설정\n" +
                $"  · ContainerId: \"{setup.ContainerId}\" (런타임에서 컨테이너를 찾을 때 쓰는 이름)\n" +
                $"  · 슬롯 수: {setup.SlotCount}",
                HelpBoxMessageType.Info));

            section.Add(new HelpBox(
                "인벤 가방과 장비창은 만드는 에셋이 다릅니다.\n\n" +
                "  · 가방 → InventoryConfigSO (슬롯마다 같은 규칙)\n" +
                "  · 장비 → EquipmentSetupSO (슬롯마다 Weapon, Head… 다르게)\n\n" +
                "위 Slot Layout을 쓰려면 CreateContainer()로 등록하세요.\n" +
                "InventoryConfigSO로 장비창을 만들면 슬롯별 설정이 적용되지 않습니다.",
                HelpBoxMessageType.Warning));

            section.Add(new Label("연동 순서")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginTop = 4, marginBottom = 4 }
            });
            section.Add(new Label(
                "1. CreateContainer() — 이 Setup으로 장비 컨테이너 생성\n" +
                "2. RegisterContainer() — InventoryGroup에 등록\n" +
                "3. ObjectEquipmentSystem.Init() — 장착/해제 연결")
            {
                style = { fontSize = 11, opacity = 0.85f, whiteSpace = WhiteSpace.Normal, marginBottom = 6 }
            });

            var copyRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            copyRow.Add(new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = snippet;
                Debug.Log("연동 코드가 클립보드에 복사되었습니다.");
            })
            {
                text = "코드 복사"
            });
            section.Add(copyRow);

            var codeLabel = new Label(snippet)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    fontSize = 11,
                    opacity = 0.85f,
                    marginTop = 6,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.35f),
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 8,
                    paddingRight = 8
                }
            };
            section.Add(codeLabel);

            return section;
        }

        public static VisualElement BuildValidationSection(EquipmentSetupSO setup)
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(setup.ContainerId))
                issues.Add("ContainerId가 비어 있습니다.");

            if (setup.SlotCategories == null || setup.SlotCategories.Length != setup.SlotCount)
                issues.Add($"Slot Count({setup.SlotCount})와 SlotCategories 길이가 맞지 않습니다. Slot Layout에서 '슬롯 배열 동기화'를 누르세요.");

            for (int i = 0; i < setup.SlotCount && setup.SlotCategories != null; i++)
            {
                if (i >= setup.SlotCategories.Length)
                    break;

                if (string.IsNullOrEmpty(setup.SlotCategories[i]))
                    issues.Add($"Slot {i}: 카테고리가 비어 있어 어떤 아이템이든 들어갈 수 있습니다.");
            }

            if (issues.Count == 0)
            {
                return new HelpBox("설정이 정상입니다. 아래 Integration Guide를 참고해 런타임에 등록하세요.", HelpBoxMessageType.Info);
            }

            return new HelpBox(string.Join("\n", issues), HelpBoxMessageType.Warning);
        }

        public static ItemDatabaseSO LoadLinkedItemDatabase(EquipmentSetupSO setup)
        {
            if (setup == null)
                return null;

            string path = AssetDatabase.GetAssetPath(setup);
            if (string.IsNullOrEmpty(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            string saved = EditorPrefs.GetString(ItemDatabasePrefsPrefix + guid, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                string dbPath = AssetDatabase.GUIDToAssetPath(saved);
                if (!string.IsNullOrEmpty(dbPath))
                {
                    var fromPrefs = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(dbPath);
                    if (fromPrefs != null)
                        return fromPrefs;
                }
            }

            return InventoryEditorAssetLookup.FindItemDatabaseNear(setup);
        }

        public static void SaveLinkedItemDatabase(EquipmentSetupSO setup, ItemDatabaseSO database)
        {
            if (setup == null)
                return;

            string path = AssetDatabase.GetAssetPath(setup);
            if (string.IsNullOrEmpty(path))
                return;

            string guid = AssetDatabase.AssetPathToGUID(path);
            string key = ItemDatabasePrefsPrefix + guid;

            if (database == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            string dbPath = AssetDatabase.GetAssetPath(database);
            EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(dbPath));
        }

        public static string GetCategoryLabel(string value)
        {
            for (int i = 0; i < CategoryOptions.Length; i++)
            {
                if (CategoryOptions[i].Value == (value ?? string.Empty))
                    return CategoryOptions[i].Label;
            }

            return string.IsNullOrEmpty(value) ? CategoryOptions[0].Label : value;
        }

        public static ItemDefinitionSO FindItemDefinition(ItemDatabaseSO database, int itemId)
        {
            if (database == null || itemId <= 0 || database.Items == null)
                return null;

            for (int i = 0; i < database.Items.Length; i++)
            {
                ItemDefinitionSO item = database.Items[i];
                if (item != null && item.ItemId == itemId)
                    return item;
            }

            return null;
        }

        private static List<string> GetCategoryLabels()
        {
            var labels = new List<string>(CategoryOptions.Length);
            for (int i = 0; i < CategoryOptions.Length; i++)
                labels.Add(CategoryOptions[i].Label);
            return labels;
        }

        private static int IndexOfCategoryValue(string value)
        {
            string normalized = value ?? string.Empty;
            for (int i = 0; i < CategoryOptions.Length; i++)
            {
                if (CategoryOptions[i].Value == normalized)
                    return i;
            }

            return -1;
        }

        private static string GetCategoryValue(string label)
        {
            for (int i = 0; i < CategoryOptions.Length; i++)
            {
                if (CategoryOptions[i].Label == label)
                    return CategoryOptions[i].Value;
            }

            return EquipmentSlotCategories.Any;
        }
    }
}
