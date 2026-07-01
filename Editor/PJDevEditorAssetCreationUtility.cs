using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors
{
    internal static class PJDevEditorAssetCreationUtility
    {
        public const string InventoryFolderPrefsKey = "PJDev.Editor.LastInventoryAssetFolder";
        public const string UISystemFolderPrefsKey = "PJDev.Editor.LastUISystemAssetFolder";

        private static readonly string[] PreferredProjectAssetsFolders = { "Assets/_Game", "Assets" };

        public static string GetDefaultProjectAssetsFolder()
        {
            foreach (string folder in PreferredProjectAssetsFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    return folder;
            }

            return "Assets";
        }

        public static string GetLastOrDefaultFolder(string prefsKey, string fallback = null)
        {
            fallback = NormalizeAssetsFolder(fallback ?? GetDefaultProjectAssetsFolder());
            if (string.IsNullOrEmpty(prefsKey))
                return EnsureValidAssetsFolder(fallback);

            string fromPrefs = NormalizeAssetsFolder(EditorPrefs.GetString(prefsKey, fallback));
            return EnsureValidAssetsFolder(fromPrefs, prefsKey);
        }

        public static string EnsureValidAssetsFolder(string folder, string prefsKey = null)
        {
            folder = NormalizeAssetsFolder(folder);
            if (AssetDatabase.IsValidFolder(folder))
                return folder;

            if (!string.IsNullOrEmpty(prefsKey))
            {
                string stored = EditorPrefs.GetString(prefsKey, string.Empty);
                if (string.Equals(NormalizeAssetsFolder(stored), folder, StringComparison.OrdinalIgnoreCase))
                    EditorPrefs.DeleteKey(prefsKey);
            }

            return GetDefaultProjectAssetsFolder();
        }

        public static bool TryPickFolder(
            string title,
            string defaultFolder,
            string prefsKey,
            out string assetsFolder)
        {
            assetsFolder = null;
            defaultFolder = EnsureValidAssetsFolder(
                string.IsNullOrWhiteSpace(defaultFolder)
                    ? GetLastOrDefaultFolder(prefsKey)
                    : defaultFolder,
                prefsKey);

            string assetsRoot = Application.dataPath.Replace('\\', '/');
            string defaultAbsolute = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(assetsRoot) ?? assetsRoot, defaultFolder))
                .Replace('\\', '/');

            if (!Directory.Exists(defaultAbsolute))
                defaultAbsolute = assetsRoot;

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
            defaultDirectory = EnsureValidAssetsFolder(
                string.IsNullOrWhiteSpace(defaultDirectory)
                    ? GetLastOrDefaultFolder(prefsKey)
                    : defaultDirectory,
                prefsKey);

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
