using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels
{
    internal sealed class InventoryItemsPanel : InventoryEditorPanelBase
    {
        private VisualElement listHost;
        private VisualElement detailHost;
        private ScrollView detailScroll;
        private string search = string.Empty;
        private int selectedIndex = -1;

        public InventoryItemsPanel(InventoryEditorContext context) : base(context) { }

        public override string Title => "Items";

        public override void Refresh()
        {
            Root.Clear();
            selectedIndex = Mathf.Clamp(selectedIndex, -1, int.MaxValue);

            if (!Context.HasSetup && Context.ItemDatabase == null)
            {
                Root.Add(CreateMissingSetupMessage("Items 탭에서 ItemDefinitionSO를 생성/편집/삭제할 수 있습니다."));
                return;
            }

            BuildDatabaseHeader();
            if (Context.ItemDatabase == null)
            {
                Root.Add(new HelpBox("Item Database SO를 연결하거나 New DB로 생성하세요.", HelpBoxMessageType.Warning));
                return;
            }

            var split = InventoryEditorUiFactory.CreateSplitView(300);
            Root.Add(split);
            (listHost, detailHost) = InventoryEditorUiFactory.GetSplit(split);

            listHost.Add(BuildListToolbar());
            listHost.Add(new ScrollView { name = "item-list-scroll", style = { flexGrow = 1 } });

            RebuildList();
            RebuildDetail();
        }

        private void BuildDatabaseHeader()
        {
            var header = new VisualElement();
            header.AddToClassList(InventoryEditorStyles.ToolbarClass);
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;

            var dbField = new ObjectField("Item DB")
            {
                objectType = typeof(ItemDatabaseSO),
                value = Context.ItemDatabase,
                allowSceneObjects = false
            };
            dbField.style.flexGrow = 1;
            dbField.SetEnabled(Context.HasSetup);
            dbField.RegisterValueChangedCallback(evt =>
            {
                if (!Context.HasSetup)
                    return;

                Undo.RecordObject(Context.Setup, "Change Item Database");
                Context.Setup.ItemDatabase = evt.newValue as ItemDatabaseSO;
                Context.MarkDirty(Context.Setup);
                selectedIndex = -1;
                Refresh();
            });
            header.Add(dbField);
            header.Add(InventoryEditorUiFactory.CreateSearchField("Search id / name / tag", value =>
            {
                search = value;
                RebuildList();
            }));
            Root.Add(header);
        }

        private VisualElement BuildListToolbar()
        {
            return InventoryCollectionToolbar.Build(new InventoryCollectionToolbar.Options
            {
                NewLabel = "+ New Item",
                AddExistingType = typeof(ItemDefinitionSO),
                ShowDatabaseCreate = Context.ItemDatabase == null && Context.HasSetup,
                OnNew = CreateNewItem,
                OnAddExisting = obj => AddItemReference(obj as ItemDefinitionSO),
                OnDuplicate = DuplicateSelectedItem,
                OnRemoveReference = RemoveSelectedReference,
                OnDeleteAsset = DeleteSelectedAsset,
                OnMoveUp = () => MoveSelected(-1),
                OnMoveDown = () => MoveSelected(1),
                OnCreateDatabase = CreateItemDatabase,
                CanActOnSelection = () => IsValidSelection(),
                CanMoveUp = () => IsValidSelection() && selectedIndex > 0,
                CanMoveDown = () => IsValidSelection() && selectedIndex < GetItems().Length - 1
            });
        }

        private ScrollView ListScroll => listHost?.Q<ScrollView>("item-list-scroll");

        private ItemDefinitionSO[] GetItems() =>
            Context.ItemDatabase?.Items ?? Array.Empty<ItemDefinitionSO>();

        private bool IsValidSelection()
        {
            ItemDefinitionSO[] items = GetItems();
            return selectedIndex >= 0 && selectedIndex < items.Length && items[selectedIndex] != null;
        }

        private ItemDefinitionSO GetSelected() =>
            IsValidSelection() ? GetItems()[selectedIndex] : null;

        private void RebuildList()
        {
            ScrollView scroll = ListScroll;
            if (scroll == null)
                return;

            InventoryEditorUiFactory.RunPreserveScroll(scroll, () =>
            {
                scroll.Clear();
                ItemDefinitionSO[] items = GetItems();
                string query = search?.Trim() ?? string.Empty;

                for (int i = 0; i < items.Length; i++)
                {
                    ItemDefinitionSO item = items[i];
                    if (item == null)
                        continue;

                    if (!MatchesSearch(item, query))
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

                    row.Add(InventoryEditorVisuals.CreateListRowContent(
                        item,
                        item.ItemId,
                        item.DisplayName ?? item.name,
                        $"{item.ItemType} · stack {item.MaxStackSize}"));
                    scroll.Add(row);
                }
            });
        }

        private void RebuildDetail()
        {
            ItemDefinitionSO item = GetSelected();
            if (item == null)
            {
                detailHost.Clear();
                detailHost.Add(new HelpBox("왼쪽에서 아이템을 선택하세요.", HelpBoxMessageType.None));
                return;
            }

            VisualElement detail = InventoryEditorUiFactory.BeginDetailPanel(detailHost);
            detailScroll = detail as ScrollView;
            detail.Add(InventoryInspectorUi.BuildHeader(item.DisplayName ?? item.name));
            detail.Add(InventoryEditorVisuals.CreateHeroPreview(item, item.ItemId));
            detail.Add(InventoryCollectionToolbar.BuildDetailActions(item, DuplicateSelectedItem, DeleteSelectedAsset));

            SerializedObject serializedObject = new SerializedObject(item);
            detail.Add(InventoryInspectorUi.BuildItemDefinitionInspector(
                serializedObject,
                () => OnItemChanged(item, serializedObject)));
        }

        private void OnItemChanged(ItemDefinitionSO item, SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedProperties();
            InventoryEditorAssetNaming.SyncItemFileName(item);
            InventoryEditorUiFactory.ApplyAssetChanges(item);
            if (Context.ItemDatabase != null)
            {
                Context.ItemDatabase.RebuildCache();
                InventoryEditorUiFactory.ApplyAssetChanges(Context.ItemDatabase);
            }

            RebuildList();
            InventoryEditorVisuals.RefreshItemDetailChrome(detailScroll, item);
        }

        private void CreateItemDatabase()
        {
            if (!Context.HasSetup)
                return;

            Undo.RecordObject(Context.Setup, "Create Item Database");
            Context.Setup.ItemDatabase = InventoryEditorAssetActions.CreateAsset<ItemDatabaseSO>(
                Context,
                "SO_ItemDatabase",
                db => db.RebuildCache());
            Context.MarkDirty(Context.Setup);
            Refresh();
        }

        private void CreateNewItem()
        {
            ItemDefinitionSO item = InventoryEditorAssetActions.CreateAsset<ItemDefinitionSO>(
                Context,
                asset =>
                {
                    asset.ItemId = GetNextItemId();
                    asset.DisplayName = "New Item";
                },
                asset => InventoryEditorAssetNaming.ForItem(asset.DisplayName));

            AddItemReference(item);
            selectedIndex = GetItems().Length - 1;
            Refresh();
            EditorGUIUtility.PingObject(item);
        }

        private void AddItemReference(ItemDefinitionSO item)
        {
            if (item == null || Context.ItemDatabase == null)
                return;

            ItemDefinitionSO[] items = GetItems();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == item)
                    return;
            }

            Undo.RecordObject(Context.ItemDatabase, "Add Item");
            var list = new List<ItemDefinitionSO>(items) { item };
            Context.ItemDatabase.Items = list.ToArray();
            Context.ItemDatabase.RebuildCache();
            Context.MarkDirty(Context.ItemDatabase);
            selectedIndex = list.Count - 1;
            RebuildList();
            RebuildDetail();
        }

        private void DuplicateSelectedItem()
        {
            ItemDefinitionSO source = GetSelected();
            if (source == null)
                return;

            ItemDefinitionSO copy = InventoryEditorAssetActions.DuplicateAsset(
                source,
                Context,
                asset =>
                {
                    asset.ItemId = GetNextItemId();
                    asset.DisplayName = source.DisplayName + " Copy";
                },
                asset => InventoryEditorAssetNaming.ForItem(asset.DisplayName));
            if (copy == null)
                return;

            EditorUtility.SetDirty(copy);
            AddItemReference(copy);
            Refresh();
        }

        private void RemoveSelectedReference()
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.ItemDatabase, "Remove Item Reference");
            var list = new List<ItemDefinitionSO>(GetItems());
            list.RemoveAt(selectedIndex);
            Context.ItemDatabase.Items = list.ToArray();
            Context.ItemDatabase.RebuildCache();
            Context.MarkDirty(Context.ItemDatabase);
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, list.Count - 1);
            Refresh();
        }

        private void DeleteSelectedAsset()
        {
            ItemDefinitionSO item = GetSelected();
            if (item == null || !InventoryEditorAssetActions.ConfirmDeleteAsset(item))
                return;

            InventoryEditorAssetActions.RemoveReferencesTo(item, Context);
            RemoveSelectedReference();
            InventoryEditorAssetActions.DeleteAssetFile(item);
            Refresh();
        }

        private void MoveSelected(int delta)
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.ItemDatabase, "Reorder Items");
            if (!InventoryEditorAssetActions.MoveArrayElement(
                GetItems(),
                selectedIndex,
                delta,
                items =>
                {
                    Context.ItemDatabase.Items = items;
                    Context.ItemDatabase.RebuildCache();
                    Context.MarkDirty(Context.ItemDatabase);
                }))
                return;

            selectedIndex += delta;
            Refresh();
        }

        private int GetNextItemId()
        {
            int max = 1000;
            ItemDefinitionSO[] items = GetItems();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].ItemId >= max)
                    max = items[i].ItemId + 1;
            }

            return max;
        }

        private static bool MatchesSearch(ItemDefinitionSO item, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            if (item.ItemId.ToString().Contains(query))
                return true;

            if (!string.IsNullOrEmpty(item.DisplayName) &&
                item.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (item.Tags == null)
                return false;

            for (int i = 0; i < item.Tags.Length; i++)
            {
                if (item.Tags[i] != null &&
                    item.Tags[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
