using System.IO;
using System.Text;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEditorAssetNaming
    {
        public static string ForItem(string displayName) => Build("SO_Item", displayName);
        public static string ForRecipe(string displayName) => Build("SO_Recipe", displayName);
        public static string ForLoot(string tableId) => Build("SO_Loot", tableId);
        public static string ForContainer(string containerId) => Build("SO_Container", containerId);

        public static void SyncItemFileName(ItemDefinitionSO item)
        {
            if (item != null)
                TrySyncAssetFileName(item, ForItem(item.DisplayName));
        }

        public static void SyncRecipeFileName(RecipeSO recipe)
        {
            if (recipe != null)
                TrySyncAssetFileName(recipe, ForRecipe(recipe.DisplayName));
        }

        public static void SyncLootFileName(LootTableSO table)
        {
            if (table != null)
                TrySyncAssetFileName(table, ForLoot(table.TableId));
        }

        public static void SyncContainerFileName(InventoryConfigSO config)
        {
            if (config != null)
                TrySyncAssetFileName(config, ForContainer(config.ContainerId));
        }

        public static string SanitizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            var builder = new StringBuilder(value.Length);
            bool previousWasSeparator = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c) || c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|')
                {
                    if (!previousWasSeparator && builder.Length > 0)
                    {
                        builder.Append('_');
                        previousWasSeparator = true;
                    }

                    continue;
                }

                builder.Append(c);
                previousWasSeparator = false;
            }

            while (builder.Length > 0 && builder[builder.Length - 1] == '_')
                builder.Length--;

            return builder.Length == 0 ? "Unnamed" : builder.ToString();
        }

        private static string Build(string prefix, string key) => $"{prefix}_{SanitizeKey(key)}";

        private static void TrySyncAssetFileName(UnityEngine.Object asset, string desiredFileName)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return;

            string current = Path.GetFileNameWithoutExtension(path);
            if (current == desiredFileName)
                return;

            string renameError = AssetDatabase.RenameAsset(path, desiredFileName);
            if (!string.IsNullOrEmpty(renameError))
            {
                string directory = Path.GetDirectoryName(path);
                string uniquePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, $"{desiredFileName}.asset"));
                string uniqueName = Path.GetFileNameWithoutExtension(uniquePath);
                AssetDatabase.RenameAsset(path, uniqueName);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
