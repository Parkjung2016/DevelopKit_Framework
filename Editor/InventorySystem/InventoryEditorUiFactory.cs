using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEditorUIFactory
    {
        public static ToolbarSearchField CreateSearchField(string placeholder, Action<string> onChanged)
        {
            var search = new ToolbarSearchField { style = { flexGrow = 1, maxWidth = 280 } };
            search.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            search.SetPlaceholderText(placeholder);
            return search;
        }

        public static Button CreateToolbarButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("inv-btn");
            return button;
        }

        public static VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList(InventoryEditorStyles.SectionClass);

            if (!string.IsNullOrEmpty(title))
            {
                var label = new Label(title);
                label.AddToClassList("inv-section-title");
                section.Add(label);
            }

            return section;
        }

        public static VisualElement CreateSplitView(float leftWidth = 280f)
        {
            var split = new VisualElement();
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            split.style.minHeight = 240;

            var left = new VisualElement();
            left.AddToClassList(InventoryEditorStyles.SplitLeftClass);
            left.style.width = leftWidth;
            left.style.flexShrink = 0;
            left.style.flexDirection = FlexDirection.Column;
            left.style.flexGrow = 0;

            var right = new VisualElement();
            right.AddToClassList(InventoryEditorStyles.SplitRightClass);
            right.style.flexGrow = 1;
            right.style.flexShrink = 1;
            right.style.minWidth = 280;
            right.style.flexDirection = FlexDirection.Column;

            split.Add(left);
            split.Add(right);
            split.userData = new SplitRefs(left, right);
            return split;
        }

        public static void ConfigurePanelRoot(VisualElement root)
        {
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;
        }

        public static VisualElement BeginDetailPanel(VisualElement detailHost)
        {
            detailHost.Clear();
            detailHost.style.flexGrow = 1;
            detailHost.style.minWidth = 280;

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "inv-detail-scroll",
                style = { flexGrow = 1 }
            };
            detailHost.Add(scroll);
            return scroll;
        }

        public static Vector2 CaptureScrollOffset(ScrollView scroll) =>
            scroll != null ? scroll.scrollOffset : Vector2.zero;

        public static void RestoreScrollOffset(ScrollView scroll, Vector2 offset)
        {
            if (scroll == null)
                return;

            scroll.scrollOffset = offset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset);
        }

        public static void RunPreserveScroll(ScrollView scroll, Action action)
        {
            if (scroll == null)
            {
                action?.Invoke();
                return;
            }

            Vector2 offset = CaptureScrollOffset(scroll);
            action?.Invoke();
            RestoreScrollOffset(scroll, offset);
        }

        public static SerializedProperty FindSerializedProperty(SerializedObject serializedObject, string propertyName)
        {
            if (serializedObject == null || string.IsNullOrEmpty(propertyName))
                return null;

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                return property;

            property = serializedObject.FindProperty($"<{propertyName}>k__BackingField");
            if (property != null)
                return property;

            SerializedProperty iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
                return null;

            do
            {
                if (iterator.name == propertyName ||
                    iterator.name == $"<{propertyName}>k__BackingField")
                    return iterator.Copy();
            }
            while (iterator.NextVisible(false));

            return null;
        }

        private static void AppendArrayPropertyField(
            VisualElement root,
            SerializedObject serializedObject,
            string propertyName,
            Action onChanged)
        {
            SerializedProperty property = FindSerializedProperty(serializedObject, propertyName);
            var section = CreateSection(propertyName);
            if (property == null)
            {
                section.Add(new HelpBox(
                    $"Property '{propertyName}' was not found on {serializedObject.targetObject.GetType().Name}.",
                    HelpBoxMessageType.Warning));
                root.Add(section);
                return;
            }

            var imgui = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                SerializedProperty current = FindSerializedProperty(serializedObject, propertyName);
                if (current == null)
                    return;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(current, includeChildren: true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    onChanged?.Invoke();
                }
            });
            imgui.style.marginTop = 2;
            imgui.style.marginBottom = 6;
            section.Add(imgui);
            root.Add(section);
        }

        public static VisualElement BuildCombinedPropertyInspector(
            SerializedObject serializedObject,
            Action onChanged,
            string[] scalarPropertyNames,
            params string[] arrayPropertyNames)
        {
            var root = new VisualElement();
            serializedObject.Update();

            if (scalarPropertyNames != null && scalarPropertyNames.Length > 0)
            {
                var scalarRoot = new VisualElement();
                BindPropertyFields(scalarRoot, serializedObject, onChanged, scalarPropertyNames);
                root.Add(scalarRoot);
            }

            for (int i = 0; i < arrayPropertyNames.Length; i++)
                AppendArrayPropertyField(root, serializedObject, arrayPropertyNames[i], onChanged);

            return root;
        }

        public static void ApplyAssetChanges(UnityEngine.Object target, Action rebuildCache = null)
        {
            if (target != null)
                EditorUtility.SetDirty(target);

            rebuildCache?.Invoke();
        }

        public static (VisualElement left, VisualElement right) GetSplit(VisualElement split) =>
            ((SplitRefs)split.userData).ToTuple();

        public static void BindPropertyFields(
            VisualElement container,
            SerializedObject serializedObject,
            params string[] propertyNames) =>
            BindPropertyFields(container, serializedObject, null, propertyNames);

        public static void BindPropertyFields(
            VisualElement container,
            SerializedObject serializedObject,
            Action onChanged,
            params string[] propertyNames)
        {
            container.Clear();
            serializedObject.Update();

            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = FindSerializedProperty(serializedObject, propertyNames[i]);
                if (property == null)
                    continue;

                if (UsesDelayedCommit(property))
                {
                    container.Add(CreateDelayedStringField(property, serializedObject, onChanged));
                    continue;
                }

                var field = new PropertyField(property);
                field.Bind(serializedObject);
                BindImmediatePropertyFieldCallbacks(field, serializedObject, onChanged);
                container.Add(field);
            }
        }

        private static bool UsesDelayedCommit(SerializedProperty property) =>
            property.propertyType == SerializedPropertyType.String;

        private static bool IsMultilineStringProperty(SerializedProperty property)
        {
            string name = property.name;
            return name == "Description" || name == "<Description>k__BackingField";
        }

        private static VisualElement CreateDelayedStringField(
            SerializedProperty property,
            SerializedObject serializedObject,
            Action onChanged)
        {
            string propertyPath = property.propertyPath;
            bool multiline = IsMultilineStringProperty(property);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = multiline ? Align.FlexStart : Align.Center;
            row.style.marginBottom = 4;
            row.style.flexGrow = 1;

            var label = new Label(property.displayName);
            label.style.minWidth = 148;
            label.style.width = 148;
            label.style.flexShrink = 0;
            if (multiline)
                label.style.paddingTop = 4;

            var textField = new TextField
            {
                multiline = multiline,
                value = property.stringValue ?? string.Empty
            };
            textField.style.flexGrow = 1;
            textField.style.flexShrink = 1;
            if (multiline)
                textField.style.minHeight = 52;

            textField.tooltip = multiline ? "Ctrl+Enter로 적용" : "Enter로 적용";

            void Commit()
            {
                serializedObject.Update();
                SerializedProperty current = serializedObject.FindProperty(propertyPath);
                if (current == null)
                    return;

                current.stringValue = textField.value;
                serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            }

            textField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (multiline)
                {
                    if (evt.keyCode != KeyCode.Return || !evt.ctrlKey)
                        return;
                }
                else if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                {
                    return;
                }

                Commit();
                textField.Blur();
                evt.StopImmediatePropagation();
                textField.panel?.focusController?.IgnoreEvent(evt);
            }, TrickleDown.TrickleDown);

            row.Add(label);
            row.Add(textField);
            return row;
        }

        private static void BindImmediatePropertyFieldCallbacks(
            PropertyField field,
            SerializedObject serializedObject,
            Action onChanged)
        {
            if (onChanged == null)
                return;

            field.RegisterValueChangeCallback(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                onChanged();
            });
        }

        public static PropertyField AddBoundPropertyField(
            VisualElement container,
            SerializedObject serializedObject,
            string propertyName,
            Action onChanged)
        {
            SerializedProperty property = FindSerializedProperty(serializedObject, propertyName);
            if (property == null)
                return null;

            if (UsesDelayedCommit(property))
            {
                container.Add(CreateDelayedStringField(property, serializedObject, onChanged));
                return null;
            }

            var field = new PropertyField(property);
            field.Bind(serializedObject);
            BindImmediatePropertyFieldCallbacks(field, serializedObject, onChanged);
            container.Add(field);
            return field;
        }

        public static void ApplyStackableMaxStackState(PropertyField maxStackField, SerializedObject serializedObject)
        {
            if (maxStackField == null)
                return;

            serializedObject.Update();
            SerializedProperty stackable = FindSerializedProperty(serializedObject, "IsStackable");
            bool isStackable = stackable == null || stackable.boolValue;

            maxStackField.SetEnabled(isStackable);
            maxStackField.EnableInClassList(InventoryEditorStyles.ConditionalDisabledFieldClass, !isStackable);
            maxStackField.tooltip = isStackable
                ? string.Empty
                : "Is Stackable이 꺼져 있으면 Max Stack Size는 사용되지 않습니다 (실제 상한: 1).";
        }

        public static T CreateAssetNextToSetup<T>(InventoryEditorContext context, string filePrefix, Action<T> initialize = null)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);

            string directory = context.GetSetupAssetDirectory();
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, $"{filePrefix}.asset"));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private readonly struct SplitRefs
        {
            private readonly VisualElement left;
            private readonly VisualElement right;

            public SplitRefs(VisualElement left, VisualElement right)
            {
                this.left = left;
                this.right = right;
            }

            public (VisualElement left, VisualElement right) ToTuple() => (left, right);
        }
    }

    internal static class ToolbarSearchFieldExtensions
    {
        public static void SetPlaceholderText(this ToolbarSearchField field, string text)
        {
            field.Q<TextField>()?.SetValueWithoutNotify(string.Empty);
            field.tooltip = text;
        }
    }

    internal static class InventoryCollectionToolbar
    {
        public sealed class Options
        {
            public string NewLabel = "+ New";
            public string AddExistingLabel = "Add Existing";
            public bool ShowMoveButtons = true;
            public bool ShowDatabaseCreate;
            public Type AddExistingType;
            public Action OnNew;
            public Action<UnityEngine.Object> OnAddExisting;
            public Action OnDuplicate;
            public Action OnRemoveReference;
            public Action OnDeleteAsset;
            public Action OnMoveUp;
            public Action OnMoveDown;
            public Action OnCreateDatabase;
            public Func<bool> CanActOnSelection = () => true;
            public Func<bool> CanMoveUp;
            public Func<bool> CanMoveDown;
        }

        public static VisualElement Build(Options options)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("inv-collection-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.marginBottom = 4;

            toolbar.Add(WrapButton(options.NewLabel, options.OnNew));

            if (options.AddExistingType != null && options.OnAddExisting != null)
            {
                var picker = new ObjectField { objectType = options.AddExistingType, allowSceneObjects = false };
                picker.style.width = 180;
                picker.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == null)
                        return;

                    options.OnAddExisting(evt.newValue);
                    picker.SetValueWithoutNotify(null);
                });
                toolbar.Add(picker);
            }

            if (options.ShowDatabaseCreate && options.OnCreateDatabase != null)
                toolbar.Add(WrapButton("New DB", options.OnCreateDatabase));

            toolbar.Add(WrapButton("Duplicate", options.OnDuplicate, options.CanActOnSelection));
            toolbar.Add(WrapButton("Remove Ref", options.OnRemoveReference, options.CanActOnSelection));

            if (options.ShowMoveButtons)
            {
                toolbar.Add(WrapButton("Up", options.OnMoveUp, options.CanMoveUp ?? options.CanActOnSelection));
                toolbar.Add(WrapButton("Down", options.OnMoveDown, options.CanMoveDown ?? options.CanActOnSelection));
            }

            var delete = WrapButton("Delete Asset", options.OnDeleteAsset, options.CanActOnSelection);
            delete.AddToClassList("inv-btn-danger");
            toolbar.Add(delete);

            return toolbar;
        }

        public static VisualElement BuildDetailActions(
            UnityEngine.Object asset,
            Action onDuplicate,
            Action onDeleteAsset,
            Action onPing = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 8;
            row.style.marginBottom = 8;

            row.Add(WrapButton("Ping", onPing ?? (() => EditorGUIUtility.PingObject(asset)), () => asset != null));
            row.Add(WrapButton("Duplicate", onDuplicate, () => asset != null));

            var delete = WrapButton("Delete Asset", onDeleteAsset, () => asset != null);
            delete.AddToClassList("inv-btn-danger");
            row.Add(delete);
            return row;
        }

        private static Button WrapButton(string text, Action onClick, Func<bool> enabled = null)
        {
            var button = InventoryEditorUIFactory.CreateToolbarButton(text, () =>
            {
                if (enabled == null || enabled())
                    onClick?.Invoke();
            });

            if (enabled != null)
            {
                button.SetEnabled(enabled());
                button.schedule.Execute(() => button.SetEnabled(enabled())).Every(100);
            }

            return button;
        }
    }

    internal static class InventoryInspectorUI
    {
        public static VisualElement BuildHeader(string title, Action openDataEditor = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;

            var label = new Label(title) { name = InventoryEditorStyles.DetailHeaderTitleName };
            label.AddToClassList(InventoryEditorStyles.PanelTitleClass);
            label.style.flexGrow = 1;
            row.Add(label);

            if (openDataEditor != null)
            {
                var button = InventoryEditorUIFactory.CreateToolbarButton("Open Data Editor", openDataEditor);
                row.Add(button);
            }

            return row;
        }

        public static VisualElement BuildFullInspector(SerializedObject serializedObject, bool includeScript = false)
        {
            var root = new VisualElement();
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
                return root;

            do
            {
                if (!includeScript && iterator.name == "m_Script")
                    continue;

                var field = new PropertyField(iterator.Copy());
                field.Bind(serializedObject);
                root.Add(field);
            }
            while (iterator.NextVisible(false));

            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                serializedObject.ApplyModifiedProperties();
            });

            return root;
        }

        public static VisualElement BuildPropertyInspector(SerializedObject serializedObject, params string[] propertyNames) =>
            BuildPropertyInspector(serializedObject, null, propertyNames);

        public static VisualElement BuildPropertyInspector(
            SerializedObject serializedObject,
            Action onChanged,
            params string[] propertyNames)
        {
            var root = new VisualElement();
            InventoryEditorUIFactory.BindPropertyFields(root, serializedObject, onChanged, propertyNames);
            return root;
        }

        public static VisualElement BuildItemDefinitionInspector(
            SerializedObject serializedObject,
            Action onChanged)
        {
            var root = new VisualElement();
            serializedObject.Update();

            PropertyField maxStackField = null;

            void RefreshMaxStackState() =>
                InventoryEditorUIFactory.ApplyStackableMaxStackState(maxStackField, serializedObject);

            Action notify = () =>
            {
                onChanged?.Invoke();
                RefreshMaxStackState();
            };

            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "ItemId", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "DisplayName", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "Description", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "Icon", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "ItemType", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "IsStackable", notify);
            maxStackField = InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "MaxStackSize", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "CanDrop", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "CanTrade", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "Weight", notify);
            InventoryEditorUIFactory.AddBoundPropertyField(root, serializedObject, "Tags", notify);

            RefreshMaxStackState();
            return root;
        }
    }

    internal static class InventoryEditorVisuals
    {
        public enum SlotSize
        {
            Small = 32,
            Medium = 40,
            Large = 48,
            Hero = 64
        }

        public static Label CreateEllipsisLabel(
            string text,
            bool bold = false,
            int fontSize = 0,
            string tooltip = null)
        {
            var label = new Label(text ?? string.Empty);
            label.AddToClassList(InventoryEditorStyles.TextEllipsisClass);
            if (bold)
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (fontSize > 0)
                label.style.fontSize = fontSize;

            string tip = tooltip ?? text;
            if (!string.IsNullOrEmpty(tip))
                label.tooltip = tip;

            return label;
        }

        public static VisualElement CreateItemSlot(
            ItemDefinitionSO item,
            int itemId,
            int count = 0,
            SlotSize size = SlotSize.Large,
            bool showCount = true,
            Sprite iconOverride = null)
        {
            int px = (int)size;
            var slot = new VisualElement();
            slot.AddToClassList(InventoryEditorStyles.ItemSlotClass);
            slot.style.width = px;
            slot.style.height = px;
            slot.style.flexShrink = 0;
            slot.style.position = Position.Relative;

            Texture iconTexture = iconOverride != null
                ? iconOverride.texture
                : item?.Icon != null ? item.Icon.texture : null;
            if (iconTexture != null)
            {
                int imageSize = Mathf.Max(20, px - 10);
                var image = new Image
                {
                    image = iconTexture,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.AddToClassList(InventoryEditorStyles.ItemSlotImageClass);
                image.style.width = imageSize;
                image.style.height = imageSize;
                slot.Add(image);
            }
            else if (itemId > 0)
            {
                slot.Add(new Label("?")
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        opacity = 0.45f,
                        fontSize = px > 40 ? 18 : 14,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                });
            }

            if (showCount && count > 1)
            {
                var countLabel = new Label(FormatCount(count));
                countLabel.AddToClassList(InventoryEditorStyles.ItemSlotCountClass);
                countLabel.pickingMode = PickingMode.Ignore;
                slot.Add(countLabel);
            }

            return slot;
        }

        public static VisualElement CreateEmptySlot(SlotSize size = SlotSize.Large)
        {
            int px = (int)size;
            var slot = new VisualElement();
            slot.AddToClassList(InventoryEditorStyles.ItemSlotClass);
            slot.AddToClassList(InventoryEditorStyles.ItemSlotEmptyClass);
            slot.style.width = px;
            slot.style.height = px;
            slot.style.flexShrink = 0;
            return slot;
        }

        public static VisualElement CreatePaletteBlock(ItemDefinitionSO item, int itemId)
        {
            var block = new VisualElement();
            block.AddToClassList(InventoryEditorStyles.ItemPaletteBlockClass);
            block.Add(CreateItemSlot(item, itemId, 0, SlotSize.Medium, showCount: false));

            string title = item != null ? item.DisplayName ?? item.name : $"#{itemId}";
            var nameLabel = CreateEllipsisLabel(
                title,
                tooltip: item != null ? $"{item.DisplayName}\nID {item.ItemId}" : $"Item #{itemId}");
            nameLabel.AddToClassList(InventoryEditorStyles.PaletteLabelClass);
            nameLabel.pickingMode = PickingMode.Ignore;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            block.Add(nameLabel);

            return block;
        }

        public static VisualElement CreateEntryInfo(ItemDefinitionSO item, int itemId)
        {
            var info = new VisualElement();
            info.AddToClassList(InventoryEditorStyles.EntryInfoClass);

            string title = item != null ? item.DisplayName ?? item.name : $"Item #{itemId}";
            info.Add(CreateEllipsisLabel(title, bold: true, fontSize: 12));

            Label idLabel = CreateEllipsisLabel($"#{itemId}", fontSize: 10);
            idLabel.style.opacity = 0.65f;
            idLabel.style.marginTop = 1;
            info.Add(idLabel);

            if (item != null)
            {
                Label typeLabel = CreateEllipsisLabel(InventoryEnumCatalog.GetItemTypeDisplayName(item.ItemType), fontSize: 10);
                typeLabel.style.opacity = 0.55f;
                typeLabel.style.marginTop = 1;
                info.Add(typeLabel);
            }

            return info;
        }

        public static VisualElement CreateEntryBody(
            ItemDefinitionSO item,
            int itemId,
            int count,
            VisualElement controls)
        {
            var body = new VisualElement();
            body.AddToClassList(InventoryEditorStyles.EntryBodyClass);
            body.Add(CreateItemSlot(item, itemId, count, SlotSize.Large));
            body.Add(CreateEntryInfo(item, itemId));

            if (controls != null)
            {
                controls.style.flexGrow = 0;
                controls.style.flexShrink = 0;
                body.Add(controls);
            }

            return body;
        }

        public static string FormatItemListSubtitle(ItemDefinitionSO item)
        {
            if (item == null)
                return string.Empty;

            int stackSize = item.IsStackable
                ? (item.MaxStackSize > 0 ? item.MaxStackSize : 1)
                : 1;

            return $"{InventoryEnumCatalog.GetItemTypeDisplayName(item.ItemType)} · stack {stackSize}";
        }

        public static VisualElement CreateListRowContent(
            ItemDefinitionSO item,
            int itemId,
            string primary,
            string secondary,
            Sprite iconOverride = null)
        {
            var content = new VisualElement();
            content.AddToClassList(InventoryEditorStyles.ListRowContentClass);

            content.Add(CreateEllipsisLabel(primary, bold: true));
            if (!string.IsNullOrEmpty(secondary))
            {
                var secondaryLabel = CreateEllipsisLabel(secondary, fontSize: 11);
                secondaryLabel.style.opacity = 0.78f;
                secondaryLabel.style.marginTop = 1;
                content.Add(secondaryLabel);
            }

            var row = new VisualElement();
            row.AddToClassList(InventoryEditorStyles.ListRowInnerClass);
            row.Add(CreateItemSlot(item, itemId, 0, SlotSize.Small, showCount: false, iconOverride));
            row.Add(content);
            return row;
        }

        public static VisualElement CreateHeroPreview(
            ItemDefinitionSO item,
            int itemId,
            Sprite iconOverride = null)
        {
            var hero = CreateHeroPreviewHost();
            SetHeroPreviewContent(hero, item, itemId, iconOverride);
            return hero;
        }

        public static VisualElement CreateHeroPreviewHost()
        {
            var hero = new VisualElement { name = InventoryEditorStyles.DetailHeroName };
            hero.AddToClassList(InventoryEditorStyles.ItemHeroClass);
            return hero;
        }

        public static void SetHeroPreviewContent(
            VisualElement heroHost,
            ItemDefinitionSO item,
            int itemId,
            Sprite iconOverride = null)
        {
            if (heroHost == null)
                return;

            heroHost.Clear();
            heroHost.Add(CreateItemSlot(item, itemId, 0, SlotSize.Hero, showCount: false, iconOverride));
        }

        public static void RefreshRecipeHero(VisualElement root, RecipeSO recipe, ItemDatabaseSO database)
        {
            VisualElement hero = root?.Q<VisualElement>(InventoryEditorStyles.DetailHeroName);
            if (hero == null)
                return;

            ItemDefinitionSO previewItem = ResolveRecipePreviewItem(recipe, database);
            Sprite previewIcon = ResolveEditorPreviewIcon(recipe.EditorIcon, previewItem);
            SetHeroPreviewContent(hero, previewItem, previewItem?.ItemId ?? 0, previewIcon);
        }

        public static void RefreshLootHero(VisualElement root, LootTableSO table, ItemDatabaseSO database)
        {
            VisualElement hero = root?.Q<VisualElement>(InventoryEditorStyles.DetailHeroName);
            if (hero == null)
                return;

            ItemDefinitionSO previewItem = ResolveLootPreviewItem(table, database);
            Sprite previewIcon = ResolveEditorPreviewIcon(table.EditorIcon, previewItem);
            SetHeroPreviewContent(hero, previewItem, previewItem?.ItemId ?? 0, previewIcon);
        }

        public static void RefreshItemDetailChrome(ScrollView detailScroll, ItemDefinitionSO item)
        {
            if (detailScroll == null || item == null)
                return;

            Label title = detailScroll.Q<Label>(InventoryEditorStyles.DetailHeaderTitleName);
            if (title != null)
                title.text = item.DisplayName ?? item.name;

            VisualElement hero = detailScroll.Q<VisualElement>(InventoryEditorStyles.DetailHeroName);
            if (hero != null)
                SetHeroPreviewContent(hero, item, item.ItemId);
        }

        public static void RefreshDetailHeaderTitle(VisualElement root, string title)
        {
            Label label = root?.Q<Label>(InventoryEditorStyles.DetailHeaderTitleName);
            if (label != null)
                label.text = title ?? string.Empty;
        }

        public static Sprite ResolveEditorPreviewIcon(Sprite editorIcon, ItemDefinitionSO fallbackItem) =>
            editorIcon != null ? editorIcon : fallbackItem?.Icon;

        public static ItemDefinitionSO ResolveRecipePreviewItem(RecipeSO recipe, ItemDatabaseSO database)
        {
            if (recipe == null || database == null)
                return null;

            InventoryRecipeEntry[] rewards = recipe.Rewards;
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Length; i++)
                {
                    if (database.TryGetItem(rewards[i].ItemId, out ItemDefinitionSO item))
                        return item;
                }
            }

            InventoryRecipeEntry[] costs = recipe.Costs;
            if (costs != null)
            {
                for (int i = 0; i < costs.Length; i++)
                {
                    if (database.TryGetItem(costs[i].ItemId, out ItemDefinitionSO item))
                        return item;
                }
            }

            return null;
        }

        public static ItemDefinitionSO ResolveLootPreviewItem(LootTableSO table, ItemDatabaseSO database)
        {
            if (table == null || database == null)
                return null;

            LootEntry[] entries = table.Entries;
            if (entries == null)
                return null;

            for (int i = 0; i < entries.Length; i++)
            {
                if (database.TryGetItem(entries[i].ItemId, out ItemDefinitionSO item))
                    return item;
            }

            return null;
        }

        private static string FormatCount(int count) =>
            count > 9999 ? "9999+" : count.ToString();
    }
}
