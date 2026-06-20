using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels
{
    internal sealed class InventoryRecipesPanel : InventoryEditorPanelBase
    {
        private VisualElement listHost;
        private VisualElement detailHost;
        private string search = string.Empty;
        private int selectedIndex = -1;
        private readonly InventoryItemPickerSession itemPickerSession = new();
        private RecipeSO detailRecipe;
        private ScrollView detailScroll;
        private InventoryDetailRefreshBinding detailBinding;

        public InventoryRecipesPanel(InventoryEditorContext context) : base(context) { }

        public override string Title => "Recipes";

        public override void Refresh()
        {
            Root.Clear();

            if (!Context.HasSetup && Context.RecipeDatabase == null)
            {
                Root.Add(CreateMissingSetupMessage("Recipes 탭에서 RecipeSO를 생성/편집/삭제할 수 있습니다."));
                return;
            }

            BuildDatabaseHeader();
            if (Context.RecipeDatabase == null)
            {
                Root.Add(new HelpBox("Recipe Database SO를 연결하거나 New DB로 생성하세요.", HelpBoxMessageType.Warning));
                return;
            }

            var split = InventoryEditorUiFactory.CreateSplitView(280);
            Root.Add(split);
            (listHost, detailHost) = InventoryEditorUiFactory.GetSplit(split);

            listHost.Add(BuildListToolbar());
            listHost.Add(new ScrollView { name = "recipe-list-scroll", style = { flexGrow = 1 } });

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

            var dbField = new ObjectField("Recipe DB")
            {
                objectType = typeof(RecipeDatabaseSO),
                value = Context.RecipeDatabase,
                allowSceneObjects = false
            };
            dbField.style.flexGrow = 1;
            dbField.SetEnabled(Context.HasSetup);
            dbField.RegisterValueChangedCallback(evt =>
            {
                if (!Context.HasSetup)
                    return;

                Undo.RecordObject(Context.Setup, "Change Recipe Database");
                Context.Setup.RecipeDatabase = evt.newValue as RecipeDatabaseSO;
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
                NewLabel = "+ New Recipe",
                AddExistingType = typeof(RecipeSO),
                ShowDatabaseCreate = Context.RecipeDatabase == null && Context.HasSetup,
                OnNew = CreateNewRecipe,
                OnAddExisting = obj => AddRecipeReference(obj as RecipeSO),
                OnDuplicate = DuplicateSelectedRecipe,
                OnRemoveReference = RemoveSelectedReference,
                OnDeleteAsset = DeleteSelectedAsset,
                OnMoveUp = () => MoveSelected(-1),
                OnMoveDown = () => MoveSelected(1),
                OnCreateDatabase = CreateRecipeDatabase,
                CanActOnSelection = () => IsValidSelection(),
                CanMoveUp = () => IsValidSelection() && selectedIndex > 0,
                CanMoveDown = () => IsValidSelection() && selectedIndex < GetRecipes().Length - 1
            });

        private ScrollView ListScroll => listHost?.Q<ScrollView>("recipe-list-scroll");

        private RecipeSO[] GetRecipes() =>
            Context.RecipeDatabase?.Recipes ?? Array.Empty<RecipeSO>();

        private bool IsValidSelection()
        {
            RecipeSO[] recipes = GetRecipes();
            return selectedIndex >= 0 && selectedIndex < recipes.Length && recipes[selectedIndex] != null;
        }

        private RecipeSO GetSelected() => IsValidSelection() ? GetRecipes()[selectedIndex] : null;

        private void RebuildList()
        {
            ScrollView scroll = ListScroll;
            if (scroll == null)
                return;

            InventoryEditorUiFactory.RunPreserveScroll(scroll, () =>
            {
                scroll.Clear();
                RecipeSO[] recipes = GetRecipes();
                string query = search?.Trim() ?? string.Empty;

                for (int i = 0; i < recipes.Length; i++)
                {
                    RecipeSO recipe = recipes[i];
                    if (recipe == null || !MatchesSearch(recipe, query))
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
                        InventoryEditorVisuals.ResolveRecipePreviewItem(recipe, Context.ItemDatabase);
                    int previewId = previewItem?.ItemId ?? 0;
                    Sprite previewIcon = InventoryEditorVisuals.ResolveEditorPreviewIcon(recipe.EditorIcon, previewItem);
                    row.Add(InventoryEditorVisuals.CreateListRowContent(
                        previewItem,
                        previewId,
                        recipe.RecipeId ?? recipe.name,
                        recipe.DisplayName ?? "-",
                        previewIcon));
                    scroll.Add(row);
                }
            });
        }

        private void RebuildDetail()
        {
            RecipeSO recipe = GetSelected();
            if (recipe == null)
            {
                detailHost.Clear();
                detailHost.Add(new HelpBox("왼쪽에서 레시피를 선택하세요.", HelpBoxMessageType.None));
                return;
            }

            VisualElement detail = InventoryEditorUiFactory.BeginDetailPanel(detailHost);
            detailScroll = detail as ScrollView;

            detail.Add(InventoryInspectorUi.BuildHeader(recipe.DisplayName ?? recipe.name));
            detail.Add(InventoryCollectionToolbar.BuildDetailActions(recipe, DuplicateSelectedRecipe, DeleteSelectedAsset));

            if (detailRecipe != recipe)
            {
                itemPickerSession.ResetForNewTarget();
                detailRecipe = recipe;
            }

            SerializedObject serializedObject = new SerializedObject(recipe);
            detail.Add(InventoryItemEntryEditors.BuildRecipeDetail(
                serializedObject,
                recipe,
                Context.ItemDatabase,
                itemPickerSession,
                () => OnRecipeChanged(serializedObject, recipe),
                out detailBinding));
            detailBinding.DetailScroll = detailScroll;
        }

        private void RefreshRecipeDetailUi() => detailBinding?.RefreshStructure();

        private void OnRecipeChanged(SerializedObject serializedObject, RecipeSO recipe)
        {
            serializedObject.ApplyModifiedProperties();
            InventoryEditorAssetNaming.SyncRecipeFileName(recipe);
            InventoryEditorUiFactory.ApplyAssetChanges(recipe);
            if (Context.RecipeDatabase != null)
            {
                Context.RecipeDatabase.RebuildCache();
                InventoryEditorUiFactory.ApplyAssetChanges(Context.RecipeDatabase);
            }

            RebuildList();
            InventoryEditorVisuals.RefreshDetailHeaderTitle(detailScroll, recipe.DisplayName ?? recipe.name);
            detailBinding?.RefreshDetailChrome?.Invoke();
        }

        private void CreateRecipeDatabase()
        {
            if (!Context.HasSetup)
                return;

            Undo.RecordObject(Context.Setup, "Create Recipe Database");
            Context.Setup.RecipeDatabase = InventoryEditorAssetActions.CreateAsset<RecipeDatabaseSO>(
                Context,
                "SO_RecipeDatabase",
                db => db.RebuildCache());
            Context.MarkDirty(Context.Setup);
            Refresh();
        }

        private void CreateNewRecipe()
        {
            RecipeSO recipe = InventoryEditorAssetActions.CreateAsset<RecipeSO>(
                Context,
                asset =>
                {
                    asset.RecipeId = $"recipe_{Guid.NewGuid():N}".Substring(0, 14);
                    asset.DisplayName = "New Recipe";
                    asset.Costs = Array.Empty<InventoryRecipeEntry>();
                    asset.Rewards = Array.Empty<InventoryRecipeEntry>();
                },
                asset => InventoryEditorAssetNaming.ForRecipe(asset.DisplayName));

            AddRecipeReference(recipe);
            Refresh();
            EditorGUIUtility.PingObject(recipe);
        }

        private void AddRecipeReference(RecipeSO recipe)
        {
            if (recipe == null || Context.RecipeDatabase == null)
                return;

            RecipeSO[] recipes = GetRecipes();
            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i] == recipe)
                    return;
            }

            Undo.RecordObject(Context.RecipeDatabase, "Add Recipe");
            var list = new List<RecipeSO>(recipes) { recipe };
            Context.RecipeDatabase.Recipes = list.ToArray();
            Context.RecipeDatabase.RebuildCache();
            Context.MarkDirty(Context.RecipeDatabase);
            selectedIndex = list.Count - 1;
            RebuildList();
            RebuildDetail();
        }

        private void DuplicateSelectedRecipe()
        {
            RecipeSO source = GetSelected();
            if (source == null)
                return;

            RecipeSO copy = InventoryEditorAssetActions.DuplicateAsset(
                source,
                Context,
                asset =>
                {
                    asset.RecipeId = source.RecipeId + "_copy";
                    asset.DisplayName = source.DisplayName + " Copy";
                },
                asset => InventoryEditorAssetNaming.ForRecipe(asset.DisplayName));
            if (copy == null)
                return;

            EditorUtility.SetDirty(copy);
            AddRecipeReference(copy);
            Refresh();
        }

        private void RemoveSelectedReference()
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.RecipeDatabase, "Remove Recipe Reference");
            var list = new List<RecipeSO>(GetRecipes());
            list.RemoveAt(selectedIndex);
            Context.RecipeDatabase.Recipes = list.ToArray();
            Context.RecipeDatabase.RebuildCache();
            Context.MarkDirty(Context.RecipeDatabase);
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, list.Count - 1);
            Refresh();
        }

        private void DeleteSelectedAsset()
        {
            RecipeSO recipe = GetSelected();
            if (recipe == null || !InventoryEditorAssetActions.ConfirmDeleteAsset(recipe))
                return;

            InventoryEditorAssetActions.RemoveReferencesTo(recipe, Context);
            RemoveSelectedReference();
            InventoryEditorAssetActions.DeleteAssetFile(recipe);
            Refresh();
        }

        private void MoveSelected(int delta)
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.RecipeDatabase, "Reorder Recipes");
            if (!InventoryEditorAssetActions.MoveArrayElement(
                GetRecipes(),
                selectedIndex,
                delta,
                items =>
                {
                    Context.RecipeDatabase.Recipes = items;
                    Context.RecipeDatabase.RebuildCache();
                    Context.MarkDirty(Context.RecipeDatabase);
                }))
                return;

            selectedIndex += delta;
            Refresh();
        }

        private static bool MatchesSearch(RecipeSO recipe, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            return (recipe.RecipeId != null && recipe.RecipeId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (recipe.DisplayName != null && recipe.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
