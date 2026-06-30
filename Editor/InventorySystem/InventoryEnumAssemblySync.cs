using System;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    [InitializeOnLoad]
    internal static class InventoryEnumAssemblySync
    {
        private const double SyncCooldownSeconds = 0.75;

        private static bool syncScheduled;
        private static double nextAllowedSyncTime;

        static InventoryEnumAssemblySync()
        {
            EditorApplication.delayCall += RequestSync;
            EditorApplication.focusChanged += OnFocusChanged;
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                RequestSync();
        }

        internal static void RequestSync()
        {
            if (syncScheduled)
                return;

            syncScheduled = true;
            EditorApplication.delayCall += RunSync;
        }

        private static void RunSync()
        {
            EditorApplication.delayCall -= RunSync;

            if (EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
            {
                syncScheduled = false;
                RequestSync();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextAllowedSyncTime)
            {
                syncScheduled = false;
                EditorApplication.delayCall += RunSync;
                return;
            }

            syncScheduled = false;
            nextAllowedSyncTime = now + SyncCooldownSeconds;

            InventoryEnumAssemblyConfigurator.SyncGeneratedMode();
        }
    }

    internal sealed class InventoryEnumGeneratedAssetProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!TouchesGeneratedRoot(importedAssets)
                && !TouchesGeneratedRoot(deletedAssets)
                && !TouchesGeneratedRoot(movedAssets)
                && !TouchesGeneratedRoot(movedFromAssetPaths))
            {
                return;
            }

            InventoryEnumAssemblySync.RequestSync();
        }

        private static bool TouchesGeneratedRoot(string[] assetPaths)
        {
            if (assetPaths == null || assetPaths.Length == 0)
                return false;

            string root = NormalizeAssetPath(InventoryEnumPaths.GeneratedRoot);
            for (int i = 0; i < assetPaths.Length; i++)
            {
                if (IsUnderGeneratedRoot(assetPaths[i], root))
                    return true;
            }

            return false;
        }

        private static bool IsUnderGeneratedRoot(string assetPath, string generatedRoot)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            string normalized = NormalizeAssetPath(assetPath);
            return normalized.Equals(generatedRoot, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(generatedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string assetPath) =>
            assetPath.Replace('\\', '/').TrimEnd('/');
    }
}
