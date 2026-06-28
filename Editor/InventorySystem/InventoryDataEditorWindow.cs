using System.Collections.Generic;
using PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    public sealed class InventoryDataEditorWindow : EditorWindow
    {
        private readonly InventoryEditorContext context = new();
        private readonly List<InventoryEditorPanelBase> panels = new();
        private VisualElement contentHost;
        private VisualElement navHost;
        private ObjectField setupField;
        private InventoryEditorPanelBase activePanel;

        [MenuItem("PJDev/Inventory/Data Editor")]
        public static void Open()
        {
            var window = GetWindow<InventoryDataEditorWindow>();
            window.titleContent = new GUIContent("Inventory Data");
            window.minSize = new Vector2(960, 560);
            window.Show();
        }

        public static void Open(InventorySetupSO setup)
        {
            Open();
            var window = GetWindow<InventoryDataEditorWindow>();
            window.SetSetup(setup);
            window.SelectTab(InventoryEditorTab.Overview);
        }

        public static void OpenItemDatabase(ItemDatabaseSO database)
        {
            Open();
            var window = GetWindow<InventoryDataEditorWindow>();
            window.context.SetStandaloneItemDatabase(database);
            window.setupField?.SetValueWithoutNotify(null);
            window.SelectTab(InventoryEditorTab.Items);
        }

        public static void OpenRecipeDatabase(RecipeDatabaseSO database)
        {
            Open();
            var window = GetWindow<InventoryDataEditorWindow>();
            window.context.SetStandaloneRecipeDatabase(database);
            window.setupField?.SetValueWithoutNotify(null);
            window.SelectTab(InventoryEditorTab.Recipes);
        }

        public static void OpenLootDatabase(LootTableDatabaseSO database)
        {
            Open();
            var window = GetWindow<InventoryDataEditorWindow>();
            window.context.SetStandaloneLootDatabase(database);
            window.setupField?.SetValueWithoutNotify(null);
            window.SelectTab(InventoryEditorTab.Loot);
        }

        public static void OpenItem(ItemDefinitionSO item) => OpenParentDatabase(item, InventoryEditorTab.Items);
        public static void OpenRecipe(RecipeSO recipe) => OpenParentDatabase(recipe, InventoryEditorTab.Recipes);
        public static void OpenLootTable(LootTableSO table) => OpenParentDatabase(table, InventoryEditorTab.Loot);
        public static void OpenContainerConfig(InventoryConfigSO config) => OpenParentDatabase(config, InventoryEditorTab.Containers);

        private static void OpenParentDatabase(Object asset, InventoryEditorTab tab)
        {
            Open();
            var window = GetWindow<InventoryDataEditorWindow>();
            window.TryBindParentSetup(asset);
            window.SelectTab(tab);
        }

        private void TryBindParentSetup(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string directory = System.IO.Path.GetDirectoryName(path);
            string[] guids = AssetDatabase.FindAssets("t:InventorySetupSO", new[] { directory });
            for (int i = 0; i < guids.Length; i++)
            {
                var setup = AssetDatabase.LoadAssetAtPath<InventorySetupSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (setup != null)
                {
                    SetSetup(setup);
                    return;
                }
            }
        }

        public void SetSetup(InventorySetupSO setup)
        {
            context.SetSetup(setup);
            if (setupField != null)
                setupField.SetValueWithoutNotify(setup);

            RefreshActivePanel();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexGrow = 1;
            InventoryEditorStyleSheet.Apply(rootVisualElement);

            VisualElement root = InventoryEditorWindowBuilder.Build(out setupField, out navHost, out contentHost);
            rootVisualElement.Add(root);

            setupField.objectType = typeof(InventorySetupSO);
            setupField.allowSceneObjects = false;
            setupField.RegisterValueChangedCallback(evt =>
            {
                context.SetSetup(evt.newValue as InventorySetupSO);
                RefreshActivePanel();
            });

            root.Q<Button>("save-btn")?.RegisterCallback<ClickEvent>(_ => context.Save());
            root.Q<Button>("create-setup-btn")?.RegisterCallback<ClickEvent>(_ => CreateSetupAsset());
            root.Q<Button>("create-all-btn")?.RegisterCallback<ClickEvent>(_ =>
            {
                if (context.Setup == null)
                    CreateSetupAsset();
                else
                    InventoryEditorAssetActions.CreateAndAssignDatabases(context);

                RefreshActivePanel();
            });
            root.Q<Button>("refresh-btn")?.RegisterCallback<ClickEvent>(_ => RefreshActivePanel());

            panels.Clear();
            panels.Add(new InventoryOverviewPanel(context));
            panels.Add(new InventoryItemsPanel(context));
            panels.Add(new InventoryRecipesPanel(context));
            panels.Add(new InventoryLootPanel(context));
            panels.Add(new InventoryContainersPanel(context));
            panels.Add(new InventoryEnumsPanel(context));

            BuildNavigation();

            RestoreSessionState();

            context.Changed += RefreshActivePanel;
        }

        private void RestoreSessionState()
        {
            if (context.Setup != null)
            {
                setupField.SetValueWithoutNotify(context.Setup);
            }
            else if (!HasStandaloneContext())
            {
                InventorySetupSO restored = InventoryDataEditorSession.LoadLastSetup();
                if (restored != null)
                    SetSetup(restored);
            }

            if (context.PendingTab.HasValue)
                SelectTab(context.PendingTab.Value);
            else
                SelectPanel(panels[0]);
        }

        private bool HasStandaloneContext() =>
            context.StandaloneItemDatabase != null ||
            context.StandaloneRecipeDatabase != null ||
            context.StandaloneLootDatabase != null;

        private void OnDisable() => context.Changed -= RefreshActivePanel;

        private void BuildNavigation()
        {
            navHost.Clear();
            for (int i = 0; i < panels.Count; i++)
            {
                InventoryEditorPanelBase panel = panels[i];
                var button = new Button(() => SelectPanel(panel)) { text = panel.Title };
                button.AddToClassList(InventoryEditorStyles.NavButtonClass);
                navHost.Add(button);
            }
        }

        private void SelectTab(InventoryEditorTab tab)
        {
            context.PendingTab = tab;
            int index = tab switch
            {
                InventoryEditorTab.Overview => 0,
                InventoryEditorTab.Items => 1,
                InventoryEditorTab.Recipes => 2,
                InventoryEditorTab.Loot => 3,
                InventoryEditorTab.Containers => 4,
                InventoryEditorTab.Enums => 5,
                _ => 0
            };

            if (index >= 0 && index < panels.Count)
                SelectPanel(panels[index]);
        }

        private void SelectPanel(InventoryEditorPanelBase panel)
        {
            activePanel = panel;
            UpdateNavHighlight();
            RefreshActivePanel();
        }

        private void UpdateNavHighlight()
        {
            foreach (VisualElement child in navHost.Children())
            {
                if (child is Button button)
                {
                    bool active = activePanel != null && button.text == activePanel.Title;
                    button.EnableInClassList(InventoryEditorStyles.NavButtonActiveClass, active);
                }
            }
        }

        private void RefreshActivePanel()
        {
            if (contentHost == null || activePanel == null)
                return;

            contentHost.Clear();
            activePanel.Build(contentHost);
            UpdateNavHighlight();
            Repaint();
        }

        private void CreateSetupAsset()
        {
            var setup = CreateInstance<InventorySetupSO>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/SO_InventorySetup.asset");
            AssetDatabase.CreateAsset(setup, path);
            AssetDatabase.SaveAssets();
            SetSetup(setup);
            EditorGUIUtility.PingObject(setup);
        }
    }

    internal static class InventoryDataEditorSession
    {
        private const string LastSetupGuidKey = "PJDev.InventoryDataEditor.LastSetupGuid";

        public static void SaveLastSetup(InventorySetupSO setup)
        {
            if (setup == null)
            {
                EditorPrefs.DeleteKey(LastSetupGuidKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(setup);
            if (string.IsNullOrEmpty(path))
            {
                EditorPrefs.DeleteKey(LastSetupGuidKey);
                return;
            }

            EditorPrefs.SetString(LastSetupGuidKey, AssetDatabase.AssetPathToGUID(path));
        }

        public static InventorySetupSO LoadLastSetup()
        {
            string guid = EditorPrefs.GetString(LastSetupGuidKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                EditorPrefs.DeleteKey(LastSetupGuidKey);
                return null;
            }

            var setup = AssetDatabase.LoadAssetAtPath<InventorySetupSO>(path);
            if (setup == null)
                EditorPrefs.DeleteKey(LastSetupGuidKey);

            return setup;
        }
    }

    internal static class InventoryDataEditorNavigation
    {
        public static void OpenAsset(UnityEngine.Object target)
        {
            if (target == null)
                return;

            switch (target)
            {
                case InventorySetupSO setup:
                    InventoryDataEditorWindow.Open(setup);
                    break;
                case ItemDatabaseSO itemDatabase:
                    InventoryDataEditorWindow.OpenItemDatabase(itemDatabase);
                    break;
                case RecipeDatabaseSO recipeDatabase:
                    InventoryDataEditorWindow.OpenRecipeDatabase(recipeDatabase);
                    break;
                case LootTableDatabaseSO lootDatabase:
                    InventoryDataEditorWindow.OpenLootDatabase(lootDatabase);
                    break;
                case ItemDefinitionSO item:
                    InventoryDataEditorWindow.OpenItem(item);
                    break;
                case RecipeSO recipe:
                    InventoryDataEditorWindow.OpenRecipe(recipe);
                    break;
                case LootTableSO lootTable:
                    InventoryDataEditorWindow.OpenLootTable(lootTable);
                    break;
                case InventoryConfigSO config:
                    InventoryDataEditorWindow.OpenContainerConfig(config);
                    break;
            }
        }
    }
}
