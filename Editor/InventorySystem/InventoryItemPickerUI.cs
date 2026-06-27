using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEditorAssetLookup
    {
        public static ItemDatabaseSO FindItemDatabaseNear(UnityEngine.Object asset)
        {
            if (asset == null)
                return null;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return null;

            string directory = System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return null;

            string[] setupGuids = AssetDatabase.FindAssets("t:InventorySetupSO", new[] { directory });
            for (int i = 0; i < setupGuids.Length; i++)
            {
                var setup = AssetDatabase.LoadAssetAtPath<InventorySetupSO>(
                    AssetDatabase.GUIDToAssetPath(setupGuids[i]));
                if (setup?.ItemDatabase != null)
                    return setup.ItemDatabase;
            }

            string[] dbGuids = AssetDatabase.FindAssets("t:ItemDatabaseSO", new[] { directory });
            if (dbGuids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(
                    AssetDatabase.GUIDToAssetPath(dbGuids[0]));
            }

            return null;
        }
    }

    internal sealed class InventoryItemPickerSession
    {
        public const string CostsKey = "Costs";
        public const string RewardsKey = "Rewards";
        public const string EntriesKey = "Entries";

        public string ActiveArray { get; private set; } = CostsKey;
        public int ActiveRowIndex { get; private set; } = -1;
        public bool ToggleMode
        {
            get => ToggleSelection.Count > 0;
            set
            {
                if (!value)
                    ClearToggleSelection();
            }
        }

        public string Search { get; set; } = string.Empty;
        public HashSet<int> ToggleSelection { get; } = new();

        public void SetActiveSlot(string arrayKey, int rowIndex)
        {
            ActiveArray = arrayKey;
            ActiveRowIndex = rowIndex;
        }

        public void ClearActiveSlot()
        {
            ActiveRowIndex = -1;
        }

        public void ToggleItem(int itemId)
        {
            if (!ToggleSelection.Add(itemId))
                ToggleSelection.Remove(itemId);
        }

        public void ClearToggleSelection() => ToggleSelection.Clear();

        public void ResetForNewTarget()
        {
            ClearActiveSlot();
            ClearToggleSelection();
        }
    }

    internal sealed class InventoryDetailRefreshBinding
    {
        public ScrollView DetailScroll;
        public Action RefreshPickerVisuals;
        public Action RefreshPickerGrid;
        public Action RefreshCosts;
        public Action RefreshRewards;
        public Action RefreshEntries;
        public Action RefreshDetailChrome;

        public void RefreshStructure()
        {
            InventoryEditorUIFactory.RunPreserveScroll(DetailScroll, () =>
            {
                RefreshDetailChrome?.Invoke();
                RefreshPickerVisuals?.Invoke();
                RefreshCosts?.Invoke();
                RefreshRewards?.Invoke();
                RefreshEntries?.Invoke();
            });
        }

        public void RefreshActiveRecipeArray(string arrayKey)
        {
            InventoryEditorUIFactory.RunPreserveScroll(DetailScroll, () =>
            {
                if (arrayKey == InventoryItemPickerSession.RewardsKey)
                    RefreshRewards?.Invoke();
                else
                    RefreshCosts?.Invoke();
            });
        }

        public void RefreshLootEntries()
        {
            InventoryEditorUIFactory.RunPreserveScroll(DetailScroll, () =>
            {
                RefreshEntries?.Invoke();
            });
        }

        public void RefreshPickerOnly()
        {
            InventoryEditorUIFactory.RunPreserveScroll(DetailScroll, RefreshPickerGrid);
        }
    }

    internal static class InventoryItemPickerUI
    {
        public const string DragItemKey = "InventoryEditor.DragItem";
        public const string DragItemsKey = "InventoryEditor.DragItems";

        public sealed class Options
        {
            public ItemDatabaseSO ItemDatabase;
            public InventoryItemPickerSession Session;
            public Action<ItemDefinitionSO> OnItemClicked;
            public Action OnRefreshPicker;
            public Action OnToggleSelectionChanged;
            public string[] AssignTargets = { InventoryItemPickerSession.CostsKey, InventoryItemPickerSession.RewardsKey };
        }

        public sealed class PickerUIHandle
        {
            public VisualElement Root;
            public Options Options;
            public VisualElement ToolbarHost;
            public VisualElement GridHost;

            public void RebuildGrid()
            {
                if (GridHost != null)
                    InventoryItemPickerUI.RebuildGrid(GridHost, Options);
            }

            public void RebuildToolbar() =>
                InventoryItemPickerUI.BuildToolbar(ToolbarHost, Options, RebuildPickerVisuals);

            public void RebuildPickerVisuals()
            {
                RebuildToolbar();
                RebuildGrid();
            }
        }

        public static PickerUIHandle BuildPicker(Options options)
        {
            var handle = new PickerUIHandle { Options = options };
            var section = InventoryEditorUIFactory.CreateSection("Item Palette");
            handle.Root = section;

            if (options.ItemDatabase == null)
            {
                section.Add(new HelpBox(
                    "Item Database가 연결되면 아이템을 클릭/드래그로 Costs·Rewards에 넣을 수 있습니다.",
                    HelpBoxMessageType.Info));
                return handle;
            }

            options.ItemDatabase.RebuildCache();

            section.Add(new Label("슬롯 클릭 → 아이템 클릭 | 아이템 드래그 → 슬롯 | Multi-select로 여러 개 한 번에 추가")
            {
                style = { opacity = 0.75f, marginBottom = 6, whiteSpace = WhiteSpace.Normal }
            });

            string assignHint = options.AssignTargets != null && options.AssignTargets.Length > 0
                ? string.Join(" / ", options.AssignTargets)
                : "Entries";
            section.Add(new Label($"대상: {assignHint}")
            {
                style = { opacity = 0.6f, marginBottom = 4, fontSize = 10 }
            });

            handle.ToolbarHost = new VisualElement();
            handle.ToolbarHost.style.flexDirection = FlexDirection.Row;
            handle.ToolbarHost.style.flexWrap = Wrap.Wrap;
            handle.ToolbarHost.style.alignItems = Align.Center;
            handle.ToolbarHost.style.marginBottom = 6;
            section.Add(handle.ToolbarHost);

            handle.GridHost = new VisualElement { name = "item-picker-grid" };
            handle.GridHost.AddToClassList(InventoryEditorStyles.PaletteGridClass);
            handle.GridHost.style.flexDirection = FlexDirection.Row;
            handle.GridHost.style.flexWrap = Wrap.Wrap;
            section.Add(handle.GridHost);

            handle.RebuildPickerVisuals();
            return handle;
        }

        public static VisualElement Build(Options options) => BuildPicker(options).Root;

        private static void BuildToolbar(VisualElement toolbar, Options options, Action rebuildPickerVisuals)
        {
            toolbar.Clear();

            toolbar.Add(InventoryEditorUIFactory.CreateSearchField("Search items", value =>
            {
                options.Session.Search = value;
                options.OnRefreshPicker?.Invoke();
            }));

            toolbar.Add(new Label("Shift+클릭: 다중 선택")
            {
                style =
                {
                    fontSize = 11,
                    opacity = 0.72f,
                    marginLeft = 6,
                    alignSelf = Align.Center
                }
            });

            if (options.Session.ToggleSelection.Count > 0)
            {
                for (int i = 0; i < options.AssignTargets.Length; i++)
                {
                    string target = options.AssignTargets[i];
                    toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton(
                        $"→ {target} ({options.Session.ToggleSelection.Count})",
                        () =>
                        {
                            BulkAssignFromToggle(options, target);
                            rebuildPickerVisuals?.Invoke();
                        }));
                }

                toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Clear", () =>
                {
                    options.Session.ClearToggleSelection();
                    rebuildPickerVisuals?.Invoke();
                }));
            }
        }

        public static void RebuildGrid(VisualElement grid, Options options)
        {
            grid.Clear();
            ItemDefinitionSO[] items = options.ItemDatabase.Items ?? Array.Empty<ItemDefinitionSO>();
            string query = options.Session.Search?.Trim() ?? string.Empty;

            for (int i = 0; i < items.Length; i++)
            {
                ItemDefinitionSO item = items[i];
                if (item == null || item.ItemId <= 0)
                    continue;

                if (!MatchesItemSearch(item, query))
                    continue;

                grid.Add(CreateItemChip(item, options));
            }

            if (grid.childCount == 0)
            {
                grid.Add(new Label("표시할 아이템이 없습니다. Items 탭에서 Item Database를 채워 주세요.")
                {
                    style = { opacity = 0.7f, whiteSpace = WhiteSpace.Normal }
                });
            }
        }

        private static VisualElement CreateItemChip(ItemDefinitionSO item, Options options)
        {
            var chip = InventoryEditorVisuals.CreatePaletteBlock(item, item.ItemId);
            chip.AddToClassList(InventoryEditorStyles.ItemChipClass);
            chip.tooltip = $"{item.DisplayName}\nID {item.ItemId}\n{item.ItemType}\nShift+클릭: 다중 선택 토글";

            bool toggled = options.Session.ToggleSelection.Contains(item.ItemId);
            chip.EnableInClassList(InventoryEditorStyles.ItemChipSelectedClass, toggled);
            chip.EnableInClassList(InventoryEditorStyles.ItemChipHoverClass, false);

            chip.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.shiftKey)
                {
                    options.Session.ToggleItem(item.ItemId);
                    RefreshChipVisualState(chip, options, item.ItemId, hovered: true);
                    options.OnToggleSelectionChanged?.Invoke();
                    evt.StopPropagation();
                    return;
                }

                if (options.Session.ToggleSelection.Count > 0)
                {
                    options.Session.ClearToggleSelection();
                    options.OnToggleSelectionChanged?.Invoke();
                }

                options.OnItemClicked?.Invoke(item);
                evt.StopPropagation();
            });

            SetupDragSource(chip, item, options);
            SetupChipPointerFeedback(chip, options, item.ItemId);
            return chip;
        }

        private static void SetupChipPointerFeedback(VisualElement chip, Options options, int itemId)
        {
            chip.RegisterCallback<PointerEnterEvent>(_ => RefreshChipVisualState(chip, options, itemId, hovered: true));
            chip.RegisterCallback<PointerLeaveEvent>(_ => RefreshChipVisualState(chip, options, itemId, hovered: false));
        }

        private static void RefreshChipVisualState(VisualElement chip, Options options, int itemId, bool hovered)
        {
            bool selected = options.Session.ToggleSelection.Contains(itemId);
            chip.EnableInClassList(InventoryEditorStyles.ItemChipSelectedClass, selected);
            chip.EnableInClassList(InventoryEditorStyles.ItemChipHoverClass, hovered && !selected);
        }

        public static void SetupDragSource(VisualElement source, ItemDefinitionSO item, Options options)
        {
            var dragState = new DragState();

            source.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                dragState.Start = (Vector2)evt.position;
                dragState.Dragging = false;
                source.CapturePointer(evt.pointerId);
            });

            source.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!source.HasPointerCapture(evt.pointerId))
                    return;

                Vector2 delta = (Vector2)evt.position - dragState.Start;
                if (dragState.Dragging || delta.magnitude < 6f)
                    return;

                dragState.Dragging = true;
                List<ItemDefinitionSO> itemsToDrag = ResolveDragItems(item, options);
                if (itemsToDrag.Count == 0)
                    return;

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragItemsKey, itemsToDrag);
                DragAndDrop.SetGenericData(DragItemKey, itemsToDrag[0]);
                DragAndDrop.objectReferences = itemsToDrag.ToArray();

                string label = itemsToDrag.Count == 1
                    ? itemsToDrag[0].DisplayName ?? itemsToDrag[0].name
                    : $"{itemsToDrag[0].DisplayName ?? itemsToDrag[0].name} +{itemsToDrag.Count - 1}";

                DragAndDrop.StartDrag(label);
                source.ReleasePointer(evt.pointerId);
            });

            source.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (source.HasPointerCapture(evt.pointerId))
                    source.ReleasePointer(evt.pointerId);
            });
        }

        private static List<ItemDefinitionSO> ResolveDragItems(ItemDefinitionSO item, Options options)
        {
            var items = new List<ItemDefinitionSO>();
            if (item == null)
                return items;

            InventoryItemPickerSession session = options?.Session;
            if (session != null &&
                session.ToggleSelection.Count > 0 &&
                session.ToggleSelection.Contains(item.ItemId))
            {
                foreach (int itemId in session.ToggleSelection)
                {
                    ItemDefinitionSO resolved = ResolveItem(options.ItemDatabase, itemId);
                    if (resolved != null)
                        items.Add(resolved);
                }
            }

            if (items.Count == 0)
                items.Add(item);

            return items;
        }

        private sealed class DragState
        {
            public Vector2 Start;
            public bool Dragging;
        }

        public static void RegisterDropTarget(VisualElement target, Action<ItemDefinitionSO> onDrop)
        {
            target.RegisterCallback<DragEnterEvent>(_ => HighlightDrop(target, true));
            target.RegisterCallback<DragLeaveEvent>(_ => HighlightDrop(target, false));

            target.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!TryGetDraggedItems(out _))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.StopPropagation();
            });

            target.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!TryGetDraggedItems(out IReadOnlyList<ItemDefinitionSO> items))
                    return;

                DragAndDrop.AcceptDrag();
                for (int i = 0; i < items.Count; i++)
                    onDrop?.Invoke(items[i]);

                HighlightDrop(target, false);
                evt.StopPropagation();
            });
        }

        public static bool TryGetDraggedItems(out IReadOnlyList<ItemDefinitionSO> items)
        {
            if (DragAndDrop.GetGenericData(DragItemsKey) is List<ItemDefinitionSO> list && list.Count > 0)
            {
                items = list;
                return true;
            }

            if (TryGetDraggedItem(out ItemDefinitionSO single))
            {
                items = new[] { single };
                return true;
            }

            items = Array.Empty<ItemDefinitionSO>();
            return false;
        }

        public static bool TryGetDraggedItem(out ItemDefinitionSO item)
        {
            item = DragAndDrop.GetGenericData(DragItemKey) as ItemDefinitionSO;
            if (item != null)
                return true;

            UnityEngine.Object[] refs = DragAndDrop.objectReferences;
            if (refs == null || refs.Length == 0)
                return false;

            item = refs[0] as ItemDefinitionSO;
            return item != null;
        }

        private static void HighlightDrop(VisualElement target, bool active) =>
            target.EnableInClassList(InventoryEditorStyles.DropTargetActiveClass, active);

        private static void BulkAssignFromToggle(Options options, string arrayKey)
        {
            if (options.Session.ToggleSelection.Count == 0)
                return;

            options.Session.SetActiveSlot(arrayKey, -1);
            foreach (int itemId in options.Session.ToggleSelection)
            {
                ItemDefinitionSO item = ResolveItem(options.ItemDatabase, itemId);
                if (item != null)
                    options.OnItemClicked?.Invoke(item);
            }

            options.Session.ClearToggleSelection();
            options.OnToggleSelectionChanged?.Invoke();
        }

        private static ItemDefinitionSO ResolveItem(ItemDatabaseSO database, int itemId)
        {
            if (database != null && database.TryGetItem(itemId, out ItemDefinitionSO item))
                return item;

            ItemDefinitionSO[] items = database?.Items ?? Array.Empty<ItemDefinitionSO>();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].ItemId == itemId)
                    return items[i];
            }

            return null;
        }

        private static bool MatchesItemSearch(ItemDefinitionSO item, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            if (item.ItemId.ToString().Contains(query))
                return true;

            if (!string.IsNullOrEmpty(item.DisplayName) &&
                item.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }
    }

    internal static class InventoryItemEntryEditors
    {
        public static VisualElement BuildRecipeDetail(
            SerializedObject serializedObject,
            RecipeSO recipe,
            ItemDatabaseSO itemDatabase,
            InventoryItemPickerSession session,
            Action onChanged,
            out InventoryDetailRefreshBinding refreshBinding)
        {
            refreshBinding = new InventoryDetailRefreshBinding();
            var binding = refreshBinding;
            var root = new VisualElement();
            serializedObject.Update();

            var scalarRoot = new VisualElement();
            InventoryEditorUIFactory.BindPropertyFields(
                scalarRoot,
                serializedObject,
                onChanged,
                "RecipeId",
                "DisplayName",
                "EditorIcon");
            root.Add(scalarRoot);

            VisualElement heroHost = InventoryEditorVisuals.CreateHeroPreviewHost();
            binding.RefreshDetailChrome = () => InventoryEditorVisuals.RefreshRecipeHero(root, recipe, itemDatabase);
            binding.RefreshDetailChrome();
            root.Add(heroHost);

            if (itemDatabase == null)
            {
                root.Add(new HelpBox(
                    "Overview에서 Item Database를 연결하면 비주얼 아이템 피커가 활성화됩니다.",
                    HelpBoxMessageType.Warning));
                root.Add(BuildImGuiArrayFallback(serializedObject, InventoryItemPickerSession.CostsKey, onChanged));
                root.Add(BuildImGuiArrayFallback(serializedObject, InventoryItemPickerSession.RewardsKey, onChanged));
                return root;
            }

            var pickerOptions = new InventoryItemPickerUI.Options
            {
                ItemDatabase = itemDatabase,
                Session = session,
                OnItemClicked = item =>
                {
                    if (item == null)
                        return;

                    ApplyRecipeItem(recipe, session.ActiveArray, session.ActiveRowIndex, item.ItemId, onChanged);
                    binding.RefreshActiveRecipeArray(session.ActiveArray);
                },
                OnRefreshPicker = () => binding.RefreshPickerOnly()
            };

            InventoryItemPickerUI.PickerUIHandle picker = InventoryItemPickerUI.BuildPicker(pickerOptions);
            picker.Options.OnToggleSelectionChanged = picker.RebuildPickerVisuals;
            binding.RefreshPickerGrid = picker.RebuildGrid;
            binding.RefreshPickerVisuals = picker.RebuildPickerVisuals;

            VisualElement workbench = BuildRecipeWorkbench(
                recipe,
                itemDatabase,
                session,
                onChanged,
                binding.RefreshStructure,
                out RecipeWorkbenchBinding workbenchBinding);

            binding.RefreshCosts = workbenchBinding.RefreshCosts;
            binding.RefreshRewards = workbenchBinding.RefreshRewards;

            root.Add(picker.Root);
            root.Add(workbench);
            root.userData = binding;
            return root;
        }

        private sealed class RecipeWorkbenchBinding
        {
            public Action RefreshCosts;
            public Action RefreshRewards;
        }

        private static VisualElement BuildRecipeWorkbench(
            RecipeSO recipe,
            ItemDatabaseSO itemDatabase,
            InventoryItemPickerSession session,
            Action onChanged,
            Action refreshUI,
            out RecipeWorkbenchBinding binding)
        {
            binding = new RecipeWorkbenchBinding();
            var section = InventoryEditorUIFactory.CreateSection("제작");
            section.AddToClassList(InventoryEditorStyles.CraftingSectionClass);

            var row = new VisualElement();
            row.AddToClassList(InventoryEditorStyles.RecipeWorkbenchRowClass);

            VisualElement costsColumn = CreateRecipeColumn(
                "재료 (Costs)",
                out VisualElement costsList);
            VisualElement rewardsColumn = CreateRecipeColumn(
                "결과 (Rewards)",
                out VisualElement rewardsList);

            var arrow = new Label("→");
            arrow.AddToClassList(InventoryEditorStyles.RecipeWorkbenchArrowClass);

            void RebuildCosts()
            {
                costsList.Clear();
                InventoryRecipeEntry[] entries = GetRecipeEntries(recipe, InventoryItemPickerSession.CostsKey);

                for (int i = 0; i < entries.Length; i++)
                {
                    int index = i;
                    costsList.Add(CreateRecipeEntryRow(
                        recipe,
                        itemDatabase,
                        session,
                        InventoryItemPickerSession.CostsKey,
                        index,
                        entries[i],
                        onChanged,
                        refreshUI));
                }

                costsList.Add(CreateEmptyRecipeSlot(
                    recipe,
                    session,
                    InventoryItemPickerSession.CostsKey,
                    onChanged,
                    refreshUI));
            }

            void RebuildRewards()
            {
                rewardsList.Clear();
                InventoryRecipeEntry[] entries = GetRecipeEntries(recipe, InventoryItemPickerSession.RewardsKey);

                for (int i = 0; i < entries.Length; i++)
                {
                    int index = i;
                    rewardsList.Add(CreateRecipeEntryRow(
                        recipe,
                        itemDatabase,
                        session,
                        InventoryItemPickerSession.RewardsKey,
                        index,
                        entries[i],
                        onChanged,
                        refreshUI));
                }

                rewardsList.Add(CreateEmptyRecipeSlot(
                    recipe,
                    session,
                    InventoryItemPickerSession.RewardsKey,
                    onChanged,
                    refreshUI));
            }

            binding.RefreshCosts = RebuildCosts;
            binding.RefreshRewards = RebuildRewards;

            row.Add(costsColumn);
            row.Add(arrow);
            row.Add(rewardsColumn);
            section.Add(row);

            RebuildCosts();
            RebuildRewards();

            return section;
        }

        private static VisualElement CreateRecipeColumn(string title, out VisualElement listHost)
        {
            var column = new VisualElement();
            column.AddToClassList(InventoryEditorStyles.RecipeWorkbenchColumnClass);

            var label = new Label(title);
            label.AddToClassList("inv-section-title");
            column.Add(label);

            listHost = new VisualElement();
            column.Add(listHost);
            return column;
        }

        private static VisualElement CreateRecipeEntryRow(
            RecipeSO recipe,
            ItemDatabaseSO itemDatabase,
            InventoryItemPickerSession session,
            string arrayKey,
            int index,
            InventoryRecipeEntry entry,
            Action onChanged,
            Action refreshUI)
        {
            itemDatabase.TryGetItem(entry.ItemId, out ItemDefinitionSO item);
            var row = CreateEntryRowShell(session, arrayKey, index);

            row.Add(InventoryEditorVisuals.CreateEntryBody(
                item,
                entry.ItemId,
                entry.Count,
                CreateCountControls(
                    entry.Count,
                    value => SetRecipeEntryCount(recipe, arrayKey, index, value, onChanged),
                    () =>
                    {
                        RemoveRecipeEntry(recipe, arrayKey, index, onChanged);
                        refreshUI?.Invoke();
                    })));

            InventoryItemPickerUI.RegisterDropTarget(row, dropped =>
            {
                session.SetActiveSlot(arrayKey, index);
                ApplyRecipeItem(recipe, arrayKey, index, dropped.ItemId, onChanged);
                refreshUI?.Invoke();
            });

            return row;
        }

        private static VisualElement CreateEmptyRecipeSlot(
            RecipeSO recipe,
            InventoryItemPickerSession session,
            string arrayKey,
            Action onChanged,
            Action refreshUI)
        {
            var row = CreateEntryRowShell(session, arrayKey, -1);
            row.AddToClassList(InventoryEditorStyles.EntrySlotEmptyClass);

            var body = new VisualElement();
            body.AddToClassList(InventoryEditorStyles.EntryBodyClass);
            body.Add(InventoryEditorVisuals.CreateEmptySlot(InventoryEditorVisuals.SlotSize.Large));
            var hint = InventoryEditorVisuals.CreateEllipsisLabel("클릭 후 아이템 선택 또는 드래그", fontSize: 11);
            hint.style.opacity = 0.65f;
            hint.style.marginLeft = 8;
            hint.style.flexGrow = 1;
            body.Add(hint);
            row.Add(body);

            InventoryItemPickerUI.RegisterDropTarget(row, dropped =>
            {
                session.SetActiveSlot(arrayKey, -1);
                ApplyRecipeItem(recipe, arrayKey, -1, dropped.ItemId, onChanged);
                refreshUI?.Invoke();
            });

            return row;
        }

        public static VisualElement BuildLootDetail(
            SerializedObject serializedObject,
            LootTableSO table,
            ItemDatabaseSO itemDatabase,
            InventoryItemPickerSession session,
            Action onChanged,
            out InventoryDetailRefreshBinding refreshBinding)
        {
            refreshBinding = new InventoryDetailRefreshBinding();
            var binding = refreshBinding;
            var root = new VisualElement();
            serializedObject.Update();

            var scalarRoot = new VisualElement();
            InventoryEditorUIFactory.BindPropertyFields(
                scalarRoot,
                serializedObject,
                onChanged,
                "TableId",
                "EditorIcon",
                "RollCount",
                "AllowDuplicateRolls");
            root.Add(scalarRoot);

            root.Add(new HelpBox(
                "Editor Icon: 목록/프리뷰 전용 아이콘. 비어 있으면 첫 번째 항목의 Item Icon을 사용합니다.\n" +
                "Roll Count: 한 번 루트할 때 항목을 몇 번 뽑을지.\n" +
                "Allow Duplicate Rolls: Roll Count ≥ 2일 때 같은 항목이 여러 번 나올 수 있는지 (끄면 항목당 1번만).",
                HelpBoxMessageType.Info));

            VisualElement heroHost = InventoryEditorVisuals.CreateHeroPreviewHost();
            binding.RefreshDetailChrome = () => InventoryEditorVisuals.RefreshLootHero(root, table, itemDatabase);
            binding.RefreshDetailChrome();
            root.Add(heroHost);

            if (itemDatabase == null)
            {
                root.Add(new HelpBox(
                    "Overview에서 Item Database를 연결하면 아이템 피커가 활성화됩니다.\n" +
                    "연결 전에는 아래 기본 인스펙터로만 편집할 수 있습니다.",
                    HelpBoxMessageType.Warning));
                root.Add(BuildImGuiArrayFallback(serializedObject, InventoryItemPickerSession.EntriesKey, onChanged));
                root.userData = binding;
                return root;
            }

            session.SetActiveSlot(InventoryItemPickerSession.EntriesKey, -1);

            Action refreshEntriesUI = null;
            VisualElement entriesSection = BuildLootEntriesSection(
                table,
                itemDatabase,
                session,
                onChanged,
                () => refreshEntriesUI?.Invoke());
            binding.RefreshEntries = () => ((Action)entriesSection.userData)?.Invoke();
            refreshEntriesUI = binding.RefreshLootEntries;

            var pickerOptions = new InventoryItemPickerUI.Options
            {
                ItemDatabase = itemDatabase,
                Session = session,
                AssignTargets = new[] { InventoryItemPickerSession.EntriesKey },
                OnItemClicked = item =>
                {
                    if (item == null)
                        return;

                    ApplyLootItem(table, session.ActiveRowIndex, item.ItemId, onChanged);
                    binding.RefreshLootEntries();
                },
                OnRefreshPicker = () => binding.RefreshPickerOnly()
            };

            InventoryItemPickerUI.PickerUIHandle picker = InventoryItemPickerUI.BuildPicker(pickerOptions);
            picker.Options.OnToggleSelectionChanged = picker.RebuildPickerVisuals;
            binding.RefreshPickerGrid = picker.RebuildGrid;
            binding.RefreshPickerVisuals = picker.RebuildPickerVisuals;

            root.Add(picker.Root);
            root.Add(entriesSection);
            root.userData = binding;
            return root;
        }

        private static VisualElement BuildLootEntriesSection(
            LootTableSO table,
            ItemDatabaseSO itemDatabase,
            InventoryItemPickerSession session,
            Action onChanged,
            Action refreshUI)
        {
            var section = InventoryEditorUIFactory.CreateSection("드롭 항목 (Entries)");
            section.AddToClassList(InventoryEditorStyles.CraftingSectionClass);
            section.Add(new Label("가중치 = 상대 확률(합 100일 필요 없음, 옆 % 참고) · 최소~최대 = 드롭 개수(아이템 스택 상한 적용)")
            {
                style = { opacity = 0.7f, marginBottom = 6, whiteSpace = WhiteSpace.Normal, fontSize = 11 }
            });

            var listHost = new VisualElement();
            section.Add(listHost);

            void RebuildList()
            {
                listHost.Clear();
                LootEntry[] entries = table.Entries ?? Array.Empty<LootEntry>();

                for (int i = 0; i < entries.Length; i++)
                {
                    int index = i;
                    listHost.Add(CreateLootEntryRow(
                        table,
                        itemDatabase,
                        session,
                        index,
                        entries[i],
                        onChanged,
                        refreshUI));
                }

                listHost.Add(CreateEmptyLootSlot(table, itemDatabase, session, onChanged, refreshUI));
            }

            RebuildList();
            section.userData = (Action)RebuildList;
            return section;
        }

        private static VisualElement CreateLootEntryRow(
            LootTableSO table,
            ItemDatabaseSO itemDatabase,
            InventoryItemPickerSession session,
            int index,
            LootEntry entry,
            Action onChanged,
            Action refreshUI)
        {
            itemDatabase.TryGetItem(entry.ItemId, out ItemDefinitionSO item);
            var row = CreateEntryRowShell(session, InventoryItemPickerSession.EntriesKey, index);

            int stackCap = GetLootStackCap(item);
            float weightPercent = GetLootEntryWeightPercent(table.Entries, entry);

            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.flexShrink = 0;
            controls.style.flexGrow = 0;
            StopRowPointerPropagation(controls);

            void CommitLoot(Action<LootEntry> apply)
            {
                LootEntry[] entries = table.Entries ?? Array.Empty<LootEntry>();
                if (index < 0 || index >= entries.Length)
                    return;

                apply(entries[index]);
                onChanged?.Invoke();
                refreshUI?.Invoke();
            }

            controls.Add(CreateSteppedIntControl("최소", entry.MinCount, 1, stackCap, value =>
                CommitLoot(current => SetLootEntryField(
                    table, index, current.ItemId, value, current.MaxCount, current.Weight, null))));
            controls.Add(CreateSteppedIntControl("최대", entry.MaxCount, 1, stackCap, value =>
                CommitLoot(current => SetLootEntryField(
                    table, index, current.ItemId, current.MinCount, value, current.Weight, null))));
            controls.Add(CreateSteppedFloatControl(
                "가중치",
                entry.Weight,
                LootWeightMin,
                value => CommitLoot(current => SetLootEntryField(
                    table, index, current.ItemId, current.MinCount, current.MaxCount, value, null))));
            controls.Add(new Label($"({weightPercent:F1}%)")
            {
                tooltip = "전체 가중치 합 대비 이 항목이 뽑힐 확률",
                style = { opacity = 0.7f, fontSize = 11, marginRight = 6, minWidth = 48 }
            });

            row.Add(InventoryEditorVisuals.CreateEntryBody(item, entry.ItemId, 0, controls));
            row.Add(InventoryEditorUIFactory.CreateToolbarButton("X", () =>
            {
                RemoveLootEntry(table, index, onChanged);
                refreshUI?.Invoke();
            }));

            InventoryItemPickerUI.RegisterDropTarget(row, dropped =>
            {
                session.SetActiveSlot(InventoryItemPickerSession.EntriesKey, index);
                ApplyLootItem(table, index, dropped.ItemId, onChanged);
                refreshUI?.Invoke();
            });

            return row;
        }

        private const float LootWeightMin = 0.01f;
        private const int LootCountFallbackMax = 9999;

        private static int GetLootStackCap(ItemDefinitionSO item)
        {
            if (item == null || item.MaxStackSize <= 0)
                return LootCountFallbackMax;

            return item.IsStackable ? item.MaxStackSize : 1;
        }

        private static float GetLootEntryWeightPercent(LootEntry[] entries, LootEntry entry)
        {
            if (entry.Weight <= 0f || entries == null || entries.Length == 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Weight > 0f)
                    total += entries[i].Weight;
            }

            return total > 0f ? entry.Weight / total * 100f : 0f;
        }

        private static VisualElement CreateSteppedIntControl(
            string label,
            int value,
            int minValue,
            int maxValue,
            Action<int> onCommit)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginRight = 8;
            group.style.marginBottom = 2;
            StopRowPointerPropagation(group);

            var caption = new Label(label);
            caption.style.width = 42;
            caption.style.fontSize = 11;
            caption.style.opacity = 0.85f;
            group.Add(caption);

            var field = new IntegerField { value = value, style = { width = 48 } };
            field.tooltip = $"{label} · {minValue}~{maxValue} · 클릭 입력 또는 좌우 드래그";
            var state = new ScrubFieldState { CommittedValue = value };

            void Commit(int next)
            {
                next = Mathf.Clamp(next, minValue, maxValue);
                field.SetValueWithoutNotify(next);
                if (state.CommittedValue == next)
                    return;

                state.CommittedValue = next;
                onCommit?.Invoke(next);
            }

            var minus = InventoryEditorUIFactory.CreateToolbarButton("-", () => Commit(field.value - 1));
            var plus = InventoryEditorUIFactory.CreateToolbarButton("+", () => Commit(field.value + 1));
            StopRowPointerPropagation(minus);
            StopRowPointerPropagation(plus);
            StopRowPointerPropagation(field);

            field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(field.value);
                field.Blur();
                evt.StopPropagation();
            });

            SetupScrubField(field, state, Commit, minValue);

            group.Add(minus);
            group.Add(field);
            group.Add(plus);
            return group;
        }

        private static VisualElement CreateSteppedFloatControl(
            string label,
            float value,
            float minValue,
            Action<float> onCommit,
            float? maxValue = null)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginRight = 4;
            group.style.marginBottom = 2;
            StopRowPointerPropagation(group);

            var caption = new Label(label);
            caption.style.width = 42;
            caption.style.fontSize = 11;
            caption.style.opacity = 0.85f;
            group.Add(caption);

            var field = new FloatField { value = value, style = { width = 52 } };
            field.tooltip = maxValue.HasValue
                ? $"{label} · {minValue}~{maxValue.Value} · 클릭 입력 또는 좌우 드래그"
                : $"{label} · {minValue} 이상(상한 없음, 상대 비율) · 클릭 입력 또는 좌우 드래그";
            var state = new ScrubFieldState { CommittedFloat = value };

            void Commit(float next)
            {
                next = maxValue.HasValue
                    ? Mathf.Clamp(next, minValue, maxValue.Value)
                    : Mathf.Max(minValue, next);
                field.SetValueWithoutNotify(next);
                if (Mathf.Approximately(state.CommittedFloat, next))
                    return;

                state.CommittedFloat = next;
                onCommit?.Invoke(next);
            }

            var minus = InventoryEditorUIFactory.CreateToolbarButton("-", () => Commit(field.value - 1f));
            var plus = InventoryEditorUIFactory.CreateToolbarButton("+", () => Commit(field.value + 1f));
            StopRowPointerPropagation(minus);
            StopRowPointerPropagation(plus);
            StopRowPointerPropagation(field);

            field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(field.value);
                field.Blur();
                evt.StopPropagation();
            });

            SetupScrubFloatField(field, state, Commit);

            group.Add(minus);
            group.Add(field);
            group.Add(plus);
            return group;
        }

        private static VisualElement CreateEmptyLootSlot(
            LootTableSO table,
            ItemDatabaseSO itemDatabase,
            InventoryItemPickerSession session,
            Action onChanged,
            Action refreshUI)
        {
            var row = CreateEntryRowShell(session, InventoryItemPickerSession.EntriesKey, -1);
            row.AddToClassList(InventoryEditorStyles.EntrySlotEmptyClass);

            var body = new VisualElement();
            body.AddToClassList(InventoryEditorStyles.EntryBodyClass);
            body.Add(InventoryEditorVisuals.CreateEmptySlot(InventoryEditorVisuals.SlotSize.Large));
            var hint = InventoryEditorVisuals.CreateEllipsisLabel("클릭 후 아이템 선택 또는 드래그", fontSize: 11);
            hint.style.opacity = 0.65f;
            hint.style.marginLeft = 8;
            hint.style.flexGrow = 1;
            body.Add(hint);
            row.Add(body);

            InventoryItemPickerUI.RegisterDropTarget(row, dropped =>
            {
                session.SetActiveSlot(InventoryItemPickerSession.EntriesKey, -1);
                ApplyLootItem(table, -1, dropped.ItemId, onChanged);
                refreshUI?.Invoke();
            });

            return row;
        }

        private static VisualElement CreateEntryRowShell(
            InventoryItemPickerSession session,
            string arrayKey,
            int index)
        {
            var row = new VisualElement();
            row.AddToClassList(InventoryEditorStyles.EntryRowClass);
            row.style.width = Length.Percent(100);
            row.style.maxWidth = Length.Percent(100);
            bool active = session.ActiveArray == arrayKey && session.ActiveRowIndex == index;
            row.EnableInClassList(InventoryEditorStyles.EntryRowActiveClass, active);

            row.RegisterCallback<ClickEvent>(evt =>
            {
                session.SetActiveSlot(arrayKey, index);
                RefreshEntryRowHighlights(row.parent, session);
                evt.StopPropagation();
            });

            row.userData = new EntryRowRef(arrayKey, index);
            return row;
        }

        internal static void RefreshEntryRowHighlights(VisualElement listHost, InventoryItemPickerSession session)
        {
            if (listHost == null)
                return;

            for (int i = 0; i < listHost.childCount; i++)
            {
                VisualElement child = listHost[i];
                if (child.userData is not EntryRowRef rowRef)
                    continue;

                bool active = session.ActiveArray == rowRef.ArrayKey && session.ActiveRowIndex == rowRef.Index;
                child.EnableInClassList(InventoryEditorStyles.EntryRowActiveClass, active);
            }
        }

        private readonly struct EntryRowRef
        {
            public EntryRowRef(string arrayKey, int index)
            {
                ArrayKey = arrayKey;
                Index = index;
            }

            public string ArrayKey { get; }
            public int Index { get; }
        }

        private static VisualElement CreateCountControls(int count, Action<int> onCommit, Action onRemove)
        {
            var panel = new VisualElement();
            panel.AddToClassList("inv-count-panel");
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignItems = Align.FlexStart;
            panel.style.justifyContent = Justify.Center;
            panel.style.minWidth = 108;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            StopRowPointerPropagation(row);
            StopRowPointerPropagation(panel);

            var field = new IntegerField { value = count, style = { width = 56 } };
            field.tooltip = "Click to type · Drag horizontally to adjust";
            var state = new ScrubFieldState { CommittedValue = count };

            void Commit(int value)
            {
                value = Mathf.Max(1, value);
                field.SetValueWithoutNotify(value);
                if (state.CommittedValue == value)
                    return;

                state.CommittedValue = value;
                onCommit?.Invoke(value);
            }

            var minus = InventoryEditorUIFactory.CreateToolbarButton("-", () => Commit(field.value - 1));
            var plus = InventoryEditorUIFactory.CreateToolbarButton("+", () => Commit(field.value + 1));
            var remove = InventoryEditorUIFactory.CreateToolbarButton("X", onRemove);
            StopRowPointerPropagation(minus);
            StopRowPointerPropagation(plus);
            StopRowPointerPropagation(remove);

            field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(field.value);
                field.Blur();
                evt.StopPropagation();
            });

            SetupScrubField(field, state, Commit, 1);

            row.Add(minus);
            row.Add(field);
            row.Add(plus);
            row.Add(remove);

            panel.Add(new Label("수량") { style = { fontSize = 10, opacity = 0.65f, marginBottom = 2 } });
            panel.Add(row);
            return panel;
        }

        private static IntegerField CreateEditableIntField(
            string label,
            int value,
            float width,
            int minValue,
            Action<int> onCommit)
        {
            var field = new IntegerField(label) { value = value, style = { width = width } };
            field.tooltip = "Click to type · Drag horizontally to adjust";
            var state = new ScrubFieldState { CommittedValue = value };

            void Commit(int next)
            {
                next = Mathf.Max(minValue, next);
                field.SetValueWithoutNotify(next);
                if (state.CommittedValue == next)
                    return;

                state.CommittedValue = next;
                onCommit?.Invoke(next);
            }

            field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(field.value);
                field.Blur();
                evt.StopPropagation();
            });

            SetupScrubField(field, state, Commit, minValue);
            return field;
        }

        private static FloatField CreateEditableFloatField(
            string label,
            float value,
            float width,
            Action<float> onCommit)
        {
            var field = new FloatField(label) { value = value, style = { width = width } };
            field.tooltip = "Click to type · Drag horizontally to adjust";
            var state = new ScrubFieldState { CommittedFloat = value };

            void Commit(float next)
            {
                next = Mathf.Max(0f, next);
                field.SetValueWithoutNotify(next);
                if (Mathf.Approximately(state.CommittedFloat, next))
                    return;

                state.CommittedFloat = next;
                onCommit?.Invoke(next);
            }

            field.RegisterCallback<FocusOutEvent>(_ => Commit(field.value));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;

                Commit(field.value);
                field.Blur();
                evt.StopPropagation();
            });

            SetupScrubFloatField(field, state, Commit);
            return field;
        }

        private static void SetupScrubField(IntegerField field, ScrubFieldState state, Action<int> commit, int minValue)
        {
            field.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                state.Start = (Vector2)evt.position;
                state.StartInt = field.value;
                state.Scrubbing = false;
                field.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            field.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!field.HasPointerCapture(evt.pointerId))
                    return;

                Vector2 delta = (Vector2)evt.position - state.Start;
                if (!state.Scrubbing && delta.magnitude < 6f)
                    return;

                state.Scrubbing = true;
                int next = Mathf.Max(minValue, state.StartInt + Mathf.RoundToInt(delta.x / 6f));
                field.SetValueWithoutNotify(next);
                evt.StopPropagation();
            });

            field.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!field.HasPointerCapture(evt.pointerId))
                    return;

                field.ReleasePointer(evt.pointerId);
                if (state.Scrubbing)
                {
                    commit(field.value);
                    field.Blur();
                    evt.StopPropagation();
                }
            });
        }

        private static void SetupScrubFloatField(FloatField field, ScrubFieldState state, Action<float> commit)
        {
            field.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                state.Start = (Vector2)evt.position;
                state.StartFloat = field.value;
                state.Scrubbing = false;
                field.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            field.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!field.HasPointerCapture(evt.pointerId))
                    return;

                Vector2 delta = (Vector2)evt.position - state.Start;
                if (!state.Scrubbing && delta.magnitude < 6f)
                    return;

                state.Scrubbing = true;
                float next = Mathf.Max(0f, state.StartFloat + delta.x * 0.05f);
                field.SetValueWithoutNotify(next);
                evt.StopPropagation();
            });

            field.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!field.HasPointerCapture(evt.pointerId))
                    return;

                field.ReleasePointer(evt.pointerId);
                if (state.Scrubbing)
                {
                    commit(field.value);
                    field.Blur();
                    evt.StopPropagation();
                }
            });
        }

        private static void StopRowPointerPropagation(VisualElement element)
        {
            element.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            element.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        }

        private sealed class ScrubFieldState
        {
            public Vector2 Start;
            public int StartInt;
            public float StartFloat;
            public int CommittedValue;
            public float CommittedFloat;
            public bool Scrubbing;
        }

        private static InventoryRecipeEntry[] GetRecipeEntries(RecipeSO recipe, string arrayKey) =>
            arrayKey == InventoryItemPickerSession.RewardsKey
                ? recipe.Rewards ?? Array.Empty<InventoryRecipeEntry>()
                : recipe.Costs ?? Array.Empty<InventoryRecipeEntry>();

        private static int FindRecipeEntryIndex(List<InventoryRecipeEntry> list, int itemId, int excludeIndex = -1)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i == excludeIndex)
                    continue;

                if (list[i].ItemId == itemId)
                    return i;
            }

            return -1;
        }

        private static void ApplyRecipeItem(RecipeSO recipe, string arrayKey, int rowIndex, int itemId, Action onChanged)
        {
            Undo.RecordObject(recipe, $"Set {arrayKey} Item");
            var list = new List<InventoryRecipeEntry>(GetRecipeEntries(recipe, arrayKey));

            int existing = FindRecipeEntryIndex(list, itemId);
            if (existing >= 0)
            {
                InventoryRecipeEntry current = list[existing];
                list[existing] = new InventoryRecipeEntry(itemId, current.Count + 1);
            }
            else
            {
                list.Add(new InventoryRecipeEntry(itemId, 1));
            }

            SetRecipeEntries(recipe, arrayKey, list);
            onChanged?.Invoke();
        }

        private static void SetRecipeEntryCount(RecipeSO recipe, string arrayKey, int index, int count, Action onChanged)
        {
            Undo.RecordObject(recipe, $"Change {arrayKey} Count");
            var list = new List<InventoryRecipeEntry>(GetRecipeEntries(recipe, arrayKey));
            if (index < 0 || index >= list.Count)
                return;

            InventoryRecipeEntry entry = list[index];
            list[index] = new InventoryRecipeEntry(entry.ItemId, Mathf.Max(1, count));
            SetRecipeEntries(recipe, arrayKey, list);
            onChanged?.Invoke();
        }

        private static void RemoveRecipeEntry(RecipeSO recipe, string arrayKey, int index, Action onChanged)
        {
            Undo.RecordObject(recipe, $"Remove {arrayKey} Entry");
            var list = new List<InventoryRecipeEntry>(GetRecipeEntries(recipe, arrayKey));
            if (index < 0 || index >= list.Count)
                return;

            list.RemoveAt(index);
            SetRecipeEntries(recipe, arrayKey, list);
            onChanged?.Invoke();
        }

        private static void SetRecipeEntries(RecipeSO recipe, string arrayKey, List<InventoryRecipeEntry> list)
        {
            if (arrayKey == InventoryItemPickerSession.RewardsKey)
                recipe.Rewards = list.ToArray();
            else
                recipe.Costs = list.ToArray();
        }

        private static int FindLootEntryIndex(List<LootEntry> list, int itemId, int excludeIndex = -1)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i == excludeIndex)
                    continue;

                if (list[i].ItemId == itemId)
                    return i;
            }

            return -1;
        }

        private static void ApplyLootItem(LootTableSO table, int rowIndex, int itemId, Action onChanged)
        {
            Undo.RecordObject(table, "Set Loot Entry Item");
            var list = new List<LootEntry>(table.Entries ?? Array.Empty<LootEntry>());

            int existing = FindLootEntryIndex(list, itemId);
            if (existing >= 0)
            {
                LootEntry current = list[existing];
                int newMax = current.MaxCount + 1;
                list[existing] = new LootEntry
                {
                    ItemId = itemId,
                    MinCount = current.MinCount,
                    MaxCount = Mathf.Max(current.MinCount, newMax),
                    Weight = current.Weight
                };
            }
            else
            {
                list.Add(new LootEntry { ItemId = itemId, MinCount = 1, MaxCount = 1, Weight = 1f });
            }

            table.Entries = list.ToArray();
            onChanged?.Invoke();
        }

        private static void SetLootEntryField(
            LootTableSO table,
            int index,
            int itemId,
            int minCount,
            int maxCount,
            float weight,
            Action onChanged)
        {
            Undo.RecordObject(table, "Edit Loot Entry");
            var list = new List<LootEntry>(table.Entries ?? Array.Empty<LootEntry>());
            if (index < 0 || index >= list.Count)
                return;

            list[index] = new LootEntry
            {
                ItemId = itemId,
                MinCount = Mathf.Clamp(minCount, 1, LootCountFallbackMax),
                MaxCount = Mathf.Clamp(Mathf.Max(minCount, maxCount), 1, LootCountFallbackMax),
                Weight = Mathf.Max(LootWeightMin, weight)
            };
            table.Entries = list.ToArray();
            onChanged?.Invoke();
        }

        private static void RemoveLootEntry(LootTableSO table, int index, Action onChanged)
        {
            Undo.RecordObject(table, "Remove Loot Entry");
            var list = new List<LootEntry>(table.Entries ?? Array.Empty<LootEntry>());
            if (index < 0 || index >= list.Count)
                return;

            list.RemoveAt(index);
            table.Entries = list.ToArray();
            onChanged?.Invoke();
        }

        private static VisualElement BuildImGuiArrayFallback(SerializedObject serializedObject, string propertyName, Action onChanged)
        {
            var section = InventoryEditorUIFactory.CreateSection(propertyName);
            var imgui = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                SerializedProperty property = InventoryEditorUIFactory.FindSerializedProperty(serializedObject, propertyName);
                if (property == null)
                    return;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(property, includeChildren: true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    onChanged?.Invoke();
                }
            });
            section.Add(imgui);
            return section;
        }
    }
}
