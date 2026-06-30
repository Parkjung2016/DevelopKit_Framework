using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels
{
    internal sealed class InventoryOverviewPanel : InventoryEditorPanelBase
    {
        public InventoryOverviewPanel(InventoryEditorContext context) : base(context) { }

        public override string Title => "Overview";

        public override void Refresh()
        {
            Root.Clear();

            if (Context.Setup == null)
            {
                Root.Add(CreateMissingSetupMessage("Overview에서 Setup과 DB를 한 번에 관리합니다."));
                return;
            }

            Root.Add(InventoryInspectorUI.BuildHeader(
                Context.Setup.name,
                () => InventoryDataEditorWindow.Open(Context.Setup)));

            SerializedObject setupObject = new SerializedObject(Context.Setup);
            Root.Add(InventoryInspectorUI.BuildPropertyInspector(
                setupObject,
                () => Context.MarkDirty(Context.Setup),
                "ContainerConfigs"));

            Root.Add(CreateDatabaseRow<ItemDatabaseSO>(
                "Item Database",
                Context.ItemDatabase,
                value =>
                {
                    Undo.RecordObject(Context.Setup, "Change Item Database");
                    Context.Setup.ItemDatabase = value;
                    Context.MarkDirty(Context.Setup);
                    Refresh();
                },
                () => CreateDatabase<ItemDatabaseSO>("SO_ItemDatabase", db => db.RebuildCache(), db => Context.Setup.ItemDatabase = db)));

            Root.Add(CreateDatabaseRow<RecipeDatabaseSO>(
                "Recipe Database",
                Context.RecipeDatabase,
                value =>
                {
                    Undo.RecordObject(Context.Setup, "Change Recipe Database");
                    Context.Setup.RecipeDatabase = value;
                    Context.MarkDirty(Context.Setup);
                    Refresh();
                },
                () => CreateDatabase<RecipeDatabaseSO>("SO_RecipeDatabase", db => db.RebuildCache(), db => Context.Setup.RecipeDatabase = db)));

            Root.Add(CreateDatabaseRow<LootTableDatabaseSO>(
                "Loot Table Database",
                Context.LootTableDatabase,
                value =>
                {
                    Undo.RecordObject(Context.Setup, "Change Loot Table Database");
                    Context.Setup.LootTableDatabase = value;
                    Context.MarkDirty(Context.Setup);
                    Refresh();
                },
                () => CreateDatabase<LootTableDatabaseSO>("SO_LootTableDatabase", db => db.RebuildCache(), db => Context.Setup.LootTableDatabase = db)));

            Root.Add(CreateStatRow("Container Configs", Context.ContainerConfigs?.Length ?? 0));
            Root.Add(CreateStatRow("Items", Context.ItemDatabase?.Items?.Length ?? 0));
            Root.Add(CreateStatRow("Recipes", Context.RecipeDatabase?.Recipes?.Length ?? 0));
            Root.Add(CreateStatRow("Loot Tables", Context.LootTableDatabase?.Tables?.Length ?? 0));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 12;

            actions.Add(InventoryEditorUIFactory.CreateToolbarButton("Create All DBs", () =>
            {
                InventoryEditorAssetActions.CreateAndAssignDatabases(Context);
                Refresh();
            }));

            actions.Add(InventoryEditorUIFactory.CreateToolbarButton("Rebuild All Caches", () =>
            {
                Context.ItemDatabase?.RebuildCache();
                Context.RecipeDatabase?.RebuildCache();
                Context.LootTableDatabase?.RebuildCache();
                Context.MarkDirty(Context.Setup);
                Refresh();
            }));

            var deleteSetup = InventoryEditorUIFactory.CreateToolbarButton("Delete Setup Asset", DeleteSetupAsset);
            deleteSetup.AddToClassList("inv-btn-danger");
            actions.Add(deleteSetup);

            Root.Add(actions);
        }

        private void CreateDatabase<T>(string prefix, System.Action<T> rebuild, System.Action<T> assign)
            where T : ScriptableObject
        {
            Undo.RecordObject(Context.Setup, $"Create {typeof(T).Name}");
            T database = InventoryEditorAssetActions.CreateAsset<T>(
                Context,
                prefix,
                rebuild,
                Context.GetSetupAssetDirectory());
            if (database == null)
                return;

            assign(database);
            Context.MarkDirty(Context.Setup);
            Refresh();
            EditorGUIUtility.PingObject(database);
        }

        private void DeleteSetupAsset()
        {
            if (!InventoryEditorAssetActions.ConfirmDeleteAsset(Context.Setup))
                return;

            InventorySetupSO setup = Context.Setup;
            Context.SetSetup(null);
            InventoryEditorAssetActions.DeleteAssetFile(setup);
        }

        private static VisualElement CreateDatabaseRow<T>(
            string label,
            T asset,
            System.Action<T> assign,
            System.Action createNew)
            where T : Object
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var field = new ObjectField(label)
            {
                objectType = typeof(T),
                value = asset,
                allowSceneObjects = false
            };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => assign?.Invoke(evt.newValue as T));
            row.Add(field);

            row.Add(InventoryEditorUIFactory.CreateToolbarButton("New", createNew));
            row.Add(InventoryEditorUIFactory.CreateToolbarButton("Ping", () =>
            {
                if (field.value != null)
                    EditorGUIUtility.PingObject(field.value);
            }));
            return row;
        }

        private static VisualElement CreateStatRow(string label, int count)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;
            row.Add(new Label(label) { style = { width = 180 } });
            row.Add(new Label(count.ToString()));
            return row;
        }
    }
}
