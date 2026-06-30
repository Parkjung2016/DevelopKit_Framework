using System.IO;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors
{
    internal static class PJDevEditorAssetCreationUtility
    {
        public const string InventoryFolderPrefsKey = "PJDev.Editor.LastInventoryAssetFolder";
        public const string UISystemFolderPrefsKey = "PJDev.Editor.LastUISystemAssetFolder";

        public static bool TryPickFolder(
            string title,
            string defaultFolder,
            string prefsKey,
            out string assetsFolder)
        {
            assetsFolder = null;
            defaultFolder = NormalizeAssetsFolder(
                string.IsNullOrEmpty(prefsKey)
                    ? defaultFolder
                    : EditorPrefs.GetString(prefsKey, defaultFolder ?? "Assets"));

            string assetsRoot = Application.dataPath.Replace('\\', '/');
            string defaultAbsolute = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(assetsRoot) ?? assetsRoot, defaultFolder))
                .Replace('\\', '/');

            string picked = EditorUtility.OpenFolderPanel(title, defaultAbsolute, string.Empty);
            if (string.IsNullOrEmpty(picked))
                return false;

            picked = Path.GetFullPath(picked).Replace('\\', '/');
            if (!picked.StartsWith(assetsRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Select a folder under Assets.", "OK");
                return false;
            }

            assetsFolder = ("Assets" + picked.Substring(assetsRoot.Length)).Replace('\\', '/');
            if (string.IsNullOrEmpty(assetsFolder))
                assetsFolder = "Assets";

            if (!string.IsNullOrEmpty(prefsKey))
                EditorPrefs.SetString(prefsKey, assetsFolder);

            return true;
        }

        public static bool TryPickAssetPath(
            string title,
            string defaultDirectory,
            string defaultFileName,
            string prefsKey,
            out string assetPath,
            string message = "")
        {
            assetPath = null;
            defaultDirectory = NormalizeAssetsFolder(
                string.IsNullOrEmpty(prefsKey)
                    ? defaultDirectory
                    : EditorPrefs.GetString(prefsKey, defaultDirectory ?? "Assets"));

            string suggestedPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{defaultDirectory}/{defaultFileName}.asset");

            assetPath = EditorUtility.SaveFilePanelInProject(
                title,
                Path.GetFileNameWithoutExtension(suggestedPath),
                "asset",
                message,
                defaultDirectory);

            if (string.IsNullOrEmpty(assetPath))
                return false;

            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(prefsKey) && !string.IsNullOrEmpty(folder))
                EditorPrefs.SetString(prefsKey, folder);

            return true;
        }

        private static string NormalizeAssetsFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return "Assets";

            folder = folder.Replace('\\', '/').TrimEnd('/');
            return folder.StartsWith("Assets", System.StringComparison.Ordinal) ? folder : "Assets";
        }
    }
}
