using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels
{
    internal sealed class InventoryLootPanel : InventoryEditorPanelBase
    {
        private VisualElement listHost;
        private VisualElement detailHost;
        private string search = string.Empty;
        private int selectedIndex = -1;
        private readonly InventoryItemPickerSession itemPickerSession = new();
        private LootTableSO detailTable;
        private ScrollView detailScroll;
        private InventoryDetailRefreshBinding detailBinding;

        public InventoryLootPanel(InventoryEditorContext context) : base(context) { }

        public override string Title => "Loot";

        public override void Refresh()
        {
            Root.Clear();

            if (!Context.HasSetup && Context.LootTableDatabase == null)
            {
                Root.Add(CreateMissingSetupMessage("Loot 탭에서 LootTableSO를 생성/편집/삭제할 수 있습니다."));
                return;
            }

            BuildDatabaseHeader();
            if (Context.LootTableDatabase == null)
            {
                Root.Add(new HelpBox("Loot Table Database SO를 연결하거나 New DB로 생성하세요.", HelpBoxMessageType.Warning));
                return;
            }

            var split = InventoryEditorUiFactory.CreateSplitView(280);
            Root.Add(split);
            (listHost, detailHost) = InventoryEditorUiFactory.GetSplit(split);

            listHost.Add(BuildListToolbar());
            listHost.Add(new ScrollView { name = "loot-list-scroll", style = { flexGrow = 1 } });

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

            var dbField = new ObjectField("Loot DB")
            {
                objectType = typeof(LootTableDatabaseSO),
                value = Context.LootTableDatabase,
                allowSceneObjects = false
            };
            dbField.style.flexGrow = 1;
            dbField.SetEnabled(Context.HasSetup);
            dbField.RegisterValueChangedCallback(evt =>
            {
                if (!Context.HasSetup)
                    return;

                Undo.RecordObject(Context.Setup, "Change Loot Database");
                Context.Setup.LootTableDatabase = evt.newValue as LootTableDatabaseSO;
                Context.MarkDirty(Context.Setup);
                selectedIndex = -1;
                Refresh();
            });
            header.Add(dbField);
            header.Add(InventoryEditorUiFactory.CreateSearchField("Search id / name", value =>
            {
                search = value;
                RebuildList();
            }));

            if (Context.ItemDatabase == null && Context.HasSetup)
            {
                header.Add(new HelpBox("Item Database를 Overview에서 연결하면 아이템 피커가 활성화됩니다.", HelpBoxMessageType.Info)
                {
                    style = { maxWidth = 280 }
                });
            }

            Root.Add(header);
        }

        private VisualElement BuildListToolbar() =>
            InventoryCollectionToolbar.Build(new InventoryCollectionToolbar.Options
            {
                NewLabel = "+ New Table",
                AddExistingType = typeof(LootTableSO),
                ShowDatabaseCreate = Context.LootTableDatabase == null && Context.HasSetup,
                OnNew = CreateNewTable,
                OnAddExisting = obj => AddTableReference(obj as LootTableSO),
                OnDuplicate = DuplicateSelectedTable,
                OnRemoveReference = RemoveSelectedReference,
                OnDeleteAsset = DeleteSelectedAsset,
                OnMoveUp = () => MoveSelected(-1),
                OnMoveDown = () => MoveSelected(1),
                OnCreateDatabase = CreateLootDatabase,
                CanActOnSelection = () => IsValidSelection(),
                CanMoveUp = () => IsValidSelection() && selectedIndex > 0,
                CanMoveDown = () => IsValidSelection() && selectedIndex < GetTables().Length - 1
            });

        private ScrollView ListScroll => listHost?.Q<ScrollView>("loot-list-scroll");

        private LootTableSO[] GetTables() =>
            Context.LootTableDatabase?.Tables ?? Array.Empty<LootTableSO>();

        private bool IsValidSelection()
        {
            LootTableSO[] tables = GetTables();
            return selectedIndex >= 0 && selectedIndex < tables.Length && tables[selectedIndex] != null;
        }

        private LootTableSO GetSelected() => IsValidSelection() ? GetTables()[selectedIndex] : null;

        private void RebuildList()
        {
            ScrollView scroll = ListScroll;
            if (scroll == null)
                return;

            InventoryEditorUiFactory.RunPreserveScroll(scroll, () =>
            {
                scroll.Clear();
                LootTableSO[] tables = GetTables();
                string query = search?.Trim() ?? string.Empty;

                for (int i = 0; i < tables.Length; i++)
                {
                    LootTableSO table = tables[i];
                    if (table == null || !MatchesSearch(table, query))
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

                    ItemDefinitionSO previewItem =
                        InventoryEditorVisuals.ResolveLootPreviewItem(table, Context.ItemDatabase);
                    int previewId = previewItem?.ItemId ?? 0;
                    Sprite previewIcon = InventoryEditorVisuals.ResolveEditorPreviewIcon(table.EditorIcon, previewItem);
                    row.Add(InventoryEditorVisuals.CreateListRowContent(
                        previewItem,
                        previewId,
                        table.TableId ?? table.name,
                        $"rolls {table.RollCount} · entries {table.Entries?.Length ?? 0}",
                        previewIcon));
                    scroll.Add(row);
                }
            });
        }

        private void RebuildDetail()
        {
            LootTableSO table = GetSelected();
            if (table == null)
            {
                detailHost.Clear();
                detailHost.Add(new HelpBox("왼쪽에서 루트 테이블을 선택하세요.", HelpBoxMessageType.None));
                return;
            }

            VisualElement detail = InventoryEditorUiFactory.BeginDetailPanel(detailHost);
            detailScroll = detail as ScrollView;

            detail.Add(InventoryInspectorUi.BuildHeader(table.TableId ?? table.name));
            detail.Add(InventoryCollectionToolbar.BuildDetailActions(table, DuplicateSelectedTable, DeleteSelectedAsset));

            if (detailTable != table)
            {
                itemPickerSession.ResetForNewTarget();
                detailTable = table;
            }

            SerializedObject serializedObject = new SerializedObject(table);
            detail.Add(InventoryItemEntryEditors.BuildLootDetail(
                serializedObject,
                table,
                Context.ItemDatabase,
                itemPickerSession,
                () => OnTableChanged(serializedObject, table),
                out detailBinding));
            detailBinding.DetailScroll = detailScroll;
        }

        private void RefreshLootDetailUi() => detailBinding?.RefreshStructure();

        private void OnTableChanged(SerializedObject serializedObject, LootTableSO table)
        {
            serializedObject.Update();
            serializedObject.ApplyModifiedProperties();
            InventoryEditorAssetNaming.SyncLootFileName(table);
            InventoryEditorUiFactory.ApplyAssetChanges(table);
            if (Context.LootTableDatabase != null)
            {
                Context.LootTableDatabase.RebuildCache();
                InventoryEditorUiFactory.ApplyAssetChanges(Context.LootTableDatabase);
            }

            RebuildList();
            InventoryEditorVisuals.RefreshDetailHeaderTitle(detailScroll, table.TableId ?? table.name);
            detailBinding?.RefreshDetailChrome?.Invoke();
        }

        private void CreateLootDatabase()
        {
            if (!Context.HasSetup)
                return;

            Undo.RecordObject(Context.Setup, "Create Loot Database");
            Context.Setup.LootTableDatabase = InventoryEditorAssetActions.CreateAsset<LootTableDatabaseSO>(
                Context,
                "SO_LootTableDatabase",
                db => db.RebuildCache());
            Context.MarkDirty(Context.Setup);
            Refresh();
        }

        private void CreateNewTable()
        {
            LootTableSO table = InventoryEditorAssetActions.CreateAsset<LootTableSO>(
                Context,
                asset =>
                {
                    asset.TableId = "new_loot";
                    asset.RollCount = 1;
                    asset.Entries = Array.Empty<LootEntry>();
                },
                asset => InventoryEditorAssetNaming.ForLoot(asset.TableId));

            AddTableReference(table);
            Refresh();
            EditorGUIUtility.PingObject(table);
        }

        private void AddTableReference(LootTableSO table)
        {
            if (table == null || Context.LootTableDatabase == null)
                return;

            LootTableSO[] tables = GetTables();
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i] == table)
                    return;
            }

            Undo.RecordObject(Context.LootTableDatabase, "Add Loot Table");
            var list = new List<LootTableSO>(tables) { table };
            Context.LootTableDatabase.Tables = list.ToArray();
            Context.LootTableDatabase.RebuildCache();
            Context.MarkDirty(Context.LootTableDatabase);
            selectedIndex = list.Count - 1;
            RebuildList();
            RebuildDetail();
        }

        private void DuplicateSelectedTable()
        {
            LootTableSO source = GetSelected();
            if (source == null)
                return;

            LootTableSO copy = InventoryEditorAssetActions.DuplicateAsset(
                source,
                Context,
                asset => asset.TableId = source.TableId + "_copy",
                asset => InventoryEditorAssetNaming.ForLoot(asset.TableId));
            if (copy == null)
                return;

            EditorUtility.SetDirty(copy);
            AddTableReference(copy);
            Refresh();
        }

        private void RemoveSelectedReference()
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.LootTableDatabase, "Remove Loot Table Reference");
            var list = new List<LootTableSO>(GetTables());
            list.RemoveAt(selectedIndex);
            Context.LootTableDatabase.Tables = list.ToArray();
            Context.LootTableDatabase.RebuildCache();
            Context.MarkDirty(Context.LootTableDatabase);
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, list.Count - 1);
            Refresh();
        }

        private void DeleteSelectedAsset()
        {
            LootTableSO table = GetSelected();
            if (table == null || !InventoryEditorAssetActions.ConfirmDeleteAsset(table))
                return;

            InventoryEditorAssetActions.RemoveReferencesTo(table, Context);
            RemoveSelectedReference();
            InventoryEditorAssetActions.DeleteAssetFile(table);
            Refresh();
        }

        private void MoveSelected(int delta)
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.LootTableDatabase, "Reorder Loot Tables");
            if (!InventoryEditorAssetActions.MoveArrayElement(
                GetTables(),
                selectedIndex,
                delta,
                items =>
                {
                    Context.LootTableDatabase.Tables = items;
                    Context.LootTableDatabase.RebuildCache();
                    Context.MarkDirty(Context.LootTableDatabase);
                }))
                return;

            selectedIndex += delta;
            Refresh();
        }

        private static bool MatchesSearch(LootTableSO table, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            return (table.TableId != null && table.TableId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                   || table.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
