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
            ("Default 6", new[]
            {
                EquipmentSlotCategories.Weapon,
                EquipmentSlotCategories.Head,
                EquipmentSlotCategories.Chest,
                EquipmentSlotCategories.Hands,
                EquipmentSlotCategories.Feet,
                EquipmentSlotCategories.Ring
            }),
            ("RPG 8", new[]
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
            ("Weapon Only", new[] { EquipmentSlotCategories.Weapon })
        };

        public static VisualElement BuildIntroHelpBox()
        {
            return new HelpBox(
                "Configures the equipment container (weapon / armor slots).\n\n" +
                "Workflow\n" +
                "1. Slot Layout — assign a category per slot (e.g. slot 0 = Weapon, slot 1 = Head)\n" +
                "2. Item Tags — add equip.Weapon tags on items (or use Overrides below)\n" +
                "3. Runtime — register via CreateContainer() (see Integration Guide at the bottom)",
                HelpBoxMessageType.Info);
        }

        public static VisualElement BuildPresetToolbar(EquipmentSetupSO setup, Action onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 6;

            var label = new Label("Presets");
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
                    tooltip = $"{categories.Length} slots · {string.Join(", ", categories)}"
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
                text = "Reset Defaults"
            };
            resetButton.AddToClassList("inv-btn");
            row.Add(resetButton);

            return row;
        }

        public static string FormatTagHint(string prefix, string category)
        {
            if (string.IsNullOrEmpty(category))
                return "→ accepts any item";

            return $"→ tag: {prefix}{category}";
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
                "Enter a custom slot category",
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
                "Back to preset list",
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
            field.tooltip = "Custom category name (e.g. Amulet, Belt)";

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
                "Items must carry a gameplay tag on ItemDefinitionSO.Tags:\n\n" +
                $"Format: {prefix}<category>\n" +
                $"Examples\n" +
                $"  · sword → {prefix}{EquipmentSlotCategories.Weapon}\n" +
                $"  · helmet → {prefix}{EquipmentSlotCategories.Head}\n\n" +
                "Or assign categories per item in Overrides below (no tag required).",
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

            section.Add(new Label("Assign a slot category to specific items without gameplay tags.")
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
                    "Auto-detects Item Database from a nearby Inventory Setup. Assign manually if needed.",
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
                    text = "+ Add Override"
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
                $"// Build equipment container (applies this Setup's slot rules)\n" +
                $"var equipContainer = {assetName}.CreateContainer(itemDatabase);\n" +
                $"inventoryGroup.RegisterContainer(equipContainer);\n\n" +
                $"// Wire equipment system\n" +
                $"objectEquipment.Init(owner, inventorySystem, {assetName}, effectApplier);\n\n" +
                $"// Equip from bag slot to equipment slot\n" +
                $"objectEquipment.TryEquipFromInventory(bagSlotIndex, equipSlotIndex);";

            section.Add(new HelpBox(
                "Current setup\n" +
                $"  · ContainerId: \"{setup.ContainerId}\" (lookup key at runtime)\n" +
                $"  · Slot count: {setup.SlotCount}",
                HelpBoxMessageType.Info));

            section.Add(new HelpBox(
                "Bags and equipment use different config assets.\n\n" +
                "  · Bag → InventoryConfigSO (same rule for every slot)\n" +
                "  · Equipment → EquipmentSetupSO (per-slot categories: Weapon, Head, …)\n\n" +
                "To apply the slot layout above, register with CreateContainer().\n" +
                "Using InventoryConfigSO for equipment ignores per-slot categories.",
                HelpBoxMessageType.Warning));

            section.Add(new Label("Steps")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginTop = 4, marginBottom = 4 }
            });
            section.Add(new Label(
                "1. CreateContainer() — build container from this Setup\n" +
                "2. RegisterContainer() — add to InventoryGroup\n" +
                "3. ObjectEquipmentSystem.Init() — enable equip / unequip")
            {
                style = { fontSize = 11, opacity = 0.85f, whiteSpace = WhiteSpace.Normal, marginBottom = 6 }
            });

            var copyRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            copyRow.Add(new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = snippet;
                Debug.Log("Equipment integration snippet copied to clipboard.");
            })
            {
                text = "Copy Integration Code"
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
                issues.Add("ContainerId is empty. Set the runtime lookup id for this equipment container.");

            if (setup.SlotCategories == null || setup.SlotCategories.Length != setup.SlotCount)
                issues.Add($"Slot count ({setup.SlotCount}) does not match SlotCategories length. Click 'Sync Slot Array' in Slot Layout.");

            for (int i = 0; i < setup.SlotCount && setup.SlotCategories != null; i++)
            {
                if (i >= setup.SlotCategories.Length)
                    break;

                if (string.IsNullOrEmpty(setup.SlotCategories[i]))
                    issues.Add($"Slot {i}: empty category — any item can be placed here.");
            }

            if (issues.Count == 0)
            {
                return new HelpBox("Setup looks good. See Integration Guide below to register at runtime.", HelpBoxMessageType.Info);
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
