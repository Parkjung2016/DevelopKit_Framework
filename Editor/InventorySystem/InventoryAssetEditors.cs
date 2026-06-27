using PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    [CustomEditor(typeof(InventorySetupSO))]
    public sealed class InventorySetupSOEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var setup = (InventorySetupSO)target;

            root.Add(InventoryInspectorUI.BuildHeader(
                "Inventory Setup",
                () => InventoryDataEditorWindow.Open(setup)));

            root.Add(InventoryInspectorUI.BuildFullInspector(serializedObject));
            root.Add(InventoryEditorUIFactory.CreateToolbarButton("Open Full Editor", () => InventoryDataEditorWindow.Open(setup)));
            return root;
        }
    }

    [CustomEditor(typeof(ItemDatabaseSO))]
    public sealed class ItemDatabaseSOEditor : Editor
    {
        private InventoryEditorContext context;
        private InventoryItemsPanel panel;
        private VisualElement panelHost;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var database = (ItemDatabaseSO)target;

            root.Add(InventoryInspectorUI.BuildHeader(
                "Item Database",
                () => InventoryDataEditorWindow.OpenItemDatabase(database)));

            root.Add(InventoryEditorUIFactory.CreateToolbarButton("Rebuild Cache", () =>
            {
                database.RebuildCache();
                EditorUtility.SetDirty(database);
                RebuildPanel();
            }));

            context = new InventoryEditorContext();
            context.SetStandaloneItemDatabase(database);
            panel = new InventoryItemsPanel(context);
            panelHost = new VisualElement { style = { minHeight = 360 } };
            root.Add(panelHost);
            RebuildPanel();

            root.TrackPropertyValue(InventoryEditorUIFactory.FindSerializedProperty(serializedObject, "Items"), _ => RebuildPanel());
            return root;
        }

        private void RebuildPanel()
        {
            panelHost.Clear();
            panel.Build(panelHost);
        }
    }

    [CustomEditor(typeof(ItemDefinitionSO))]
    public sealed class ItemDefinitionSOEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var item = (ItemDefinitionSO)target;

            root.Add(InventoryInspectorUI.BuildHeader("Item Definition", () => InventoryDataEditorNavigation.OpenAsset(item)));
            root.Add(InventoryCollectionToolbar.BuildDetailActions(
                item,
                () => Duplicate(item),
                () => Delete(item)));
            root.Add(InventoryInspectorUI.BuildItemDefinitionInspector(serializedObject, () =>
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }));
            return root;
        }

        private static void Duplicate(ItemDefinitionSO source)
        {
            var copy = Instantiate(source);
            copy.ItemId = source.ItemId + 1;
            copy.DisplayName = source.DisplayName + " Copy";
            string path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(source).Replace(".asset", "_Copy.asset"));
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
        }

        private static void Delete(ItemDefinitionSO item)
        {
            if (!InventoryEditorAssetActions.ConfirmDeleteAsset(item))
                return;

            InventoryEditorAssetActions.DeleteAssetFile(item);
        }
    }

    [CustomEditor(typeof(RecipeDatabaseSO))]
    public sealed class RecipeDatabaseSOEditor : Editor
    {
        private InventoryEditorContext context;
        private InventoryRecipesPanel panel;
        private VisualElement panelHost;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var database = (RecipeDatabaseSO)target;

            root.Add(InventoryInspectorUI.BuildHeader(
                "Recipe Database",
                () => InventoryDataEditorWindow.OpenRecipeDatabase(database)));

            root.Add(InventoryEditorUIFactory.CreateToolbarButton("Rebuild Cache", () =>
            {
                database.RebuildCache();
                EditorUtility.SetDirty(database);
                RebuildPanel();
            }));

            context = new InventoryEditorContext();
            context.SetStandaloneRecipeDatabase(database);
            panel = new InventoryRecipesPanel(context);
            panelHost = new VisualElement { style = { minHeight = 360 } };
            root.Add(panelHost);
            RebuildPanel();

            root.TrackPropertyValue(InventoryEditorUIFactory.FindSerializedProperty(serializedObject, "Recipes"), _ => RebuildPanel());
            return root;
        }

        private void RebuildPanel()
        {
            panelHost.Clear();
            panel.Build(panelHost);
        }
    }

    [CustomEditor(typeof(RecipeSO))]
    public sealed class RecipeSOEditor : Editor
    {
        private readonly InventoryItemPickerSession itemPickerSession = new();
        private VisualElement detailHost;
        private ScrollView detailScroll;
        private InventoryDetailRefreshBinding detailBinding;
        private ItemDatabaseSO itemDatabase;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var recipe = (RecipeSO)target;

            root.Add(InventoryInspectorUI.BuildHeader("Recipe", () => InventoryDataEditorNavigation.OpenAsset(recipe)));
            root.Add(InventoryCollectionToolbar.BuildDetailActions(
                recipe,
                () => Duplicate(recipe),
                () => Delete(recipe)));

            itemDatabase = InventoryEditorAssetLookup.FindItemDatabaseNear(recipe);

            var dbRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            var dbField = new ObjectField("Item DB")
            {
                objectType = typeof(ItemDatabaseSO),
                value = itemDatabase,
                allowSceneObjects = false,
                style = { flexGrow = 1 }
            };
            dbField.RegisterValueChangedCallback(evt =>
            {
                itemDatabase = evt.newValue as ItemDatabaseSO;
                RebuildDetail();
            });
            dbRow.Add(dbField);
            root.Add(dbRow);

            detailHost = new VisualElement();
            root.Add(detailHost);
            RebuildDetail();
            return root;
        }

        private void RebuildDetail()
        {
            detailHost.Clear();
            var recipe = (RecipeSO)target;
            SerializedObject serializedObject = new SerializedObject(recipe);

            VisualElement scroll = InventoryEditorUIFactory.BeginDetailPanel(detailHost);
            detailScroll = scroll as ScrollView;

            scroll.Add(InventoryItemEntryEditors.BuildRecipeDetail(
                serializedObject,
                recipe,
                itemDatabase,
                itemPickerSession,
                Apply,
                out detailBinding));
            detailBinding.DetailScroll = detailScroll;
        }

        private void RefreshDetail() => detailBinding?.RefreshStructure();

        private void Apply()
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void Duplicate(RecipeSO source)
        {
            var copy = Instantiate(source);
            copy.RecipeId = source.RecipeId + "_copy";
            string path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(source).Replace(".asset", "_Copy.asset"));
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
        }

        private static void Delete(RecipeSO recipe)
        {
            if (!InventoryEditorAssetActions.ConfirmDeleteAsset(recipe))
                return;

            InventoryEditorAssetActions.DeleteAssetFile(recipe);
        }
    }

    [CustomEditor(typeof(LootTableDatabaseSO))]
    public sealed class LootTableDatabaseSOEditor : Editor
    {
        private InventoryEditorContext context;
        private InventoryLootPanel panel;
        private VisualElement panelHost;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var database = (LootTableDatabaseSO)target;

            root.Add(InventoryInspectorUI.BuildHeader(
                "Loot Table Database",
                () => InventoryDataEditorWindow.OpenLootDatabase(database)));

            root.Add(InventoryEditorUIFactory.CreateToolbarButton("Rebuild Cache", () =>
            {
                database.RebuildCache();
                EditorUtility.SetDirty(database);
                RebuildPanel();
            }));

            context = new InventoryEditorContext();
            context.SetStandaloneLootDatabase(database);
            panel = new InventoryLootPanel(context);
            panelHost = new VisualElement { style = { minHeight = 360 } };
            root.Add(panelHost);
            RebuildPanel();

            root.TrackPropertyValue(InventoryEditorUIFactory.FindSerializedProperty(serializedObject, "Tables"), _ => RebuildPanel());
            return root;
        }

        private void RebuildPanel()
        {
            panelHost.Clear();
            panel.Build(panelHost);
        }
    }

    [CustomEditor(typeof(LootTableSO))]
    public sealed class LootTableSOEditor : Editor
    {
        private readonly InventoryItemPickerSession itemPickerSession = new();
        private VisualElement detailHost;
        private ScrollView detailScroll;
        private InventoryDetailRefreshBinding detailBinding;
        private ItemDatabaseSO itemDatabase;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var table = (LootTableSO)target;

            root.Add(InventoryInspectorUI.BuildHeader("Loot Table", () => InventoryDataEditorNavigation.OpenAsset(table)));
            root.Add(InventoryCollectionToolbar.BuildDetailActions(
                table,
                () => Duplicate(table),
                () => Delete(table)));

            itemDatabase = InventoryEditorAssetLookup.FindItemDatabaseNear(table);

            var dbRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            var dbField = new ObjectField("Item DB")
            {
                objectType = typeof(ItemDatabaseSO),
                value = itemDatabase,
                allowSceneObjects = false,
                style = { flexGrow = 1 }
            };
            dbField.RegisterValueChangedCallback(evt =>
            {
                itemDatabase = evt.newValue as ItemDatabaseSO;
                RebuildDetail();
            });
            dbRow.Add(dbField);
            root.Add(dbRow);

            detailHost = new VisualElement();
            root.Add(detailHost);
            RebuildDetail();
            return root;
        }

        private void RebuildDetail()
        {
            detailHost.Clear();
            var table = (LootTableSO)target;
            SerializedObject serializedObject = new SerializedObject(table);

            VisualElement scroll = InventoryEditorUIFactory.BeginDetailPanel(detailHost);
            detailScroll = scroll as ScrollView;

            scroll.Add(InventoryItemEntryEditors.BuildLootDetail(
                serializedObject,
                table,
                itemDatabase,
                itemPickerSession,
                Apply,
                out detailBinding));
            detailBinding.DetailScroll = detailScroll;
        }

        private void Apply()
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void Duplicate(LootTableSO source)
        {
            var copy = Instantiate(source);
            copy.TableId = source.TableId + "_copy";
            string path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(source).Replace(".asset", "_Copy.asset"));
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
        }

        private static void Delete(LootTableSO table)
        {
            if (!InventoryEditorAssetActions.ConfirmDeleteAsset(table))
                return;

            InventoryEditorAssetActions.DeleteAssetFile(table);
        }
    }

    [CustomEditor(typeof(InventoryConfigSO))]
    public sealed class InventoryConfigSOEditor : Editor
    {
        private VisualElement root;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            Rebuild();
            return root;
        }

        private void Rebuild()
        {
            root.Clear();
            var config = (InventoryConfigSO)target;

            root.Add(InventoryInspectorUI.BuildHeader("Container Config", () => InventoryDataEditorNavigation.OpenAsset(config)));
            root.Add(InventoryCollectionToolbar.BuildDetailActions(
                config,
                () => Duplicate(config),
                () => Delete(config)));

            SerializedObject so = serializedObject;
            root.Add(InventoryContainerRulesUI.Build(
                config,
                so,
                Apply,
                Rebuild));
        }

        private void Apply()
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void Duplicate(InventoryConfigSO source)
        {
            var copy = Instantiate(source);
            copy.ContainerId = source.ContainerId + "_copy";
            string path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(source).Replace(".asset", "_Copy.asset"));
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
        }

        private static void Delete(InventoryConfigSO config)
        {
            if (!InventoryEditorAssetActions.ConfirmDeleteAsset(config))
                return;

            InventoryEditorAssetActions.DeleteAssetFile(config);
        }
    }
}
