using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal enum InventoryEditorTab
    {
        Overview,
        Items,
        Recipes,
        Loot,
        Containers,
        Enums
    }

    internal sealed class InventoryEditorContext
    {
        public InventorySetupSO Setup { get; private set; }
        public InventoryEditorTab? PendingTab { get; set; }

        public ItemDatabaseSO StandaloneItemDatabase { get; private set; }
        public RecipeDatabaseSO StandaloneRecipeDatabase { get; private set; }
        public LootTableDatabaseSO StandaloneLootDatabase { get; private set; }

        public event Action Changed;

        public bool HasSetup => Setup != null;

        public ItemDatabaseSO ItemDatabase => StandaloneItemDatabase ?? (Setup != null ? Setup.ItemDatabase : null);
        public RecipeDatabaseSO RecipeDatabase => StandaloneRecipeDatabase ?? (Setup != null ? Setup.RecipeDatabase : null);
        public LootTableDatabaseSO LootTableDatabase => StandaloneLootDatabase ?? (Setup != null ? Setup.LootTableDatabase : null);
        public InventoryConfigSO[] ContainerConfigs =>
            Setup != null ? Setup.ContainerConfigs : Array.Empty<InventoryConfigSO>();

        public void SetSetup(InventorySetupSO setup)
        {
            Setup = setup;
            ClearStandalone();
            InventoryDataEditorSession.SaveLastSetup(setup);
            Changed?.Invoke();
        }

        public void SetStandaloneItemDatabase(ItemDatabaseSO database)
        {
            Setup = null;
            StandaloneItemDatabase = database;
            StandaloneRecipeDatabase = null;
            StandaloneLootDatabase = null;
            Changed?.Invoke();
        }

        public void SetStandaloneRecipeDatabase(RecipeDatabaseSO database)
        {
            Setup = null;
            StandaloneRecipeDatabase = database;
            StandaloneItemDatabase = InventoryEditorAssetLookup.FindItemDatabaseNear(database);
            StandaloneLootDatabase = null;
            Changed?.Invoke();
        }

        public void SetStandaloneLootDatabase(LootTableDatabaseSO database)
        {
            Setup = null;
            StandaloneLootDatabase = database;
            StandaloneItemDatabase = InventoryEditorAssetLookup.FindItemDatabaseNear(database);
            StandaloneRecipeDatabase = null;
            Changed?.Invoke();
        }

        public void ClearStandalone()
        {
            StandaloneItemDatabase = null;
            StandaloneRecipeDatabase = null;
            StandaloneLootDatabase = null;
        }

        public void MarkDirty(UnityEngine.Object target = null)
        {
            if (Setup != null)
                EditorUtility.SetDirty(Setup);

            if (target != null)
                EditorUtility.SetDirty(target);

            Changed?.Invoke();
        }

        public void Save()
        {
            if (Setup != null)
                EditorUtility.SetDirty(Setup);

            AssetDatabase.SaveAssets();
        }

        public string GetAssetDirectory() => GetSetupAssetDirectory();

        public string GetSetupAssetDirectory() =>
            GetDirectoryForObject(Setup) ?? "Assets";

        public string GetItemDatabaseDirectory() =>
            GetDirectoryForObject(ItemDatabase) ?? GetSetupAssetDirectory();

        public string GetRecipeDatabaseDirectory() =>
            GetDirectoryForObject(RecipeDatabase) ?? GetSetupAssetDirectory();

        public string GetLootDatabaseDirectory() =>
            GetDirectoryForObject(LootTableDatabase) ?? GetSetupAssetDirectory();

        private static string GetDirectoryForObject(UnityEngine.Object asset)
        {
            if (asset == null)
                return null;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return null;

            return Path.GetDirectoryName(path)?.Replace('\\', '/');
        }
    }

    internal static class InventoryEditorAssetActions
    {
        public static T CreateAsset<T>(
            InventoryEditorContext context,
            string filePrefix,
            Action<T> initialize = null,
            string directory = null)
            where T : ScriptableObject =>
            CreateAsset(context, initialize, _ => filePrefix, directory);

        public static T CreateAsset<T>(
            InventoryEditorContext context,
            Action<T> initialize,
            Func<T, string> filePrefixResolver,
            string directory = null)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);

            string filePrefix = filePrefixResolver(asset);
            string targetDirectory = directory ?? context.GetAssetDirectory();
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetDirectory, $"{filePrefix}.asset"));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static T DuplicateAsset<T>(
            T source,
            InventoryEditorContext context,
            Action<T> configureCopy,
            Func<T, string> filePrefixResolver,
            string directory = null)
            where T : ScriptableObject
        {
            if (source == null)
                return null;

            T copy = UnityEngine.Object.Instantiate(source);
            configureCopy?.Invoke(copy);

            string filePrefix = filePrefixResolver(copy);
            string targetDirectory = directory ?? context.GetAssetDirectory();
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetDirectory, $"{filePrefix}.asset"));
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            return copy;
        }

        public static bool ConfirmDeleteAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return false;

            return EditorUtility.DisplayDialog(
                "Delete Asset",
                $"'{asset.name}' asset 파일을 삭제합니다.\nProject에서 완전히 제거됩니다.",
                "Delete",
                "Cancel");
        }

        public static void DeleteAssetFile(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
        }

        public static void RemoveReferencesTo(UnityEngine.Object asset, InventoryEditorContext context)
        {
            if (asset == null || context == null)
                return;

            if (asset is ItemDefinitionSO item && context.ItemDatabase != null)
            {
                RemoveFromArray(context.ItemDatabase, context.ItemDatabase.Items, item, items =>
                {
                    context.ItemDatabase.Items = items;
                    context.ItemDatabase.RebuildCache();
                });
            }

            if (asset is RecipeSO recipe && context.RecipeDatabase != null)
            {
                RemoveFromArray(context.RecipeDatabase, context.RecipeDatabase.Recipes, recipe, items =>
                {
                    context.RecipeDatabase.Recipes = items;
                    context.RecipeDatabase.RebuildCache();
                });
            }

            if (asset is LootTableSO table && context.LootTableDatabase != null)
            {
                RemoveFromArray(context.LootTableDatabase, context.LootTableDatabase.Tables, table, items =>
                {
                    context.LootTableDatabase.Tables = items;
                    context.LootTableDatabase.RebuildCache();
                });
            }

            if (asset is InventoryConfigSO config && context.Setup != null)
            {
                RemoveFromArray(context.Setup, context.ContainerConfigs, config, items =>
                    context.Setup.ContainerConfigs = items);
            }
        }

        public static void CreateAndAssignDatabases(InventoryEditorContext context)
        {
            if (context.Setup == null)
                return;

            Undo.RecordObject(context.Setup, "Create Inventory Databases");

            if (context.Setup.ItemDatabase == null)
            {
                context.Setup.ItemDatabase = CreateAsset<ItemDatabaseSO>(
                    context,
                    "SO_ItemDatabase",
                    db => db.RebuildCache(),
                    context.GetSetupAssetDirectory());
            }

            if (context.Setup.RecipeDatabase == null)
            {
                context.Setup.RecipeDatabase = CreateAsset<RecipeDatabaseSO>(
                    context,
                    "SO_RecipeDatabase",
                    db => db.RebuildCache(),
                    context.GetSetupAssetDirectory());
            }

            if (context.Setup.LootTableDatabase == null)
            {
                context.Setup.LootTableDatabase = CreateAsset<LootTableDatabaseSO>(
                    context,
                    "SO_LootTableDatabase",
                    db => db.RebuildCache(),
                    context.GetSetupAssetDirectory());
            }

            context.MarkDirty(context.Setup);
        }

        public static bool MoveArrayElement<T>(T[] source, int index, int delta, Action<T[]> apply)
        {
            if (source == null || source.Length == 0)
                return false;

            int target = index + delta;
            if (index < 0 || index >= source.Length || target < 0 || target >= source.Length)
                return false;

            var list = new List<T>(source);
            (list[index], list[target]) = (list[target], list[index]);
            apply(list.ToArray());
            return true;
        }

        private static void RemoveFromArray<T>(
            UnityEngine.Object owner,
            T[] source,
            T target,
            Action<T[]> apply)
            where T : UnityEngine.Object
        {
            if (source == null || target == null)
                return;

            var list = new List<T>(source);
            if (!list.Remove(target))
                return;

            Undo.RecordObject(owner, "Remove Reference");
            apply(list.ToArray());
        }
    }
}
