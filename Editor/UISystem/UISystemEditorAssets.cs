using PJDev.DevelopKit.Framework.Editors;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal static class UISystemEditorAssets
    {
        internal const string CatalogPrefsKey = "PJDev.UISystemSettings.CatalogGuid";
        internal const string LayerSettingsPrefsKey = "PJDev.UISystemSettings.LayerSettingsGuid";
        internal const string LastTabPrefsKey = "PJDev.UISystemSettings.LastTab";
        internal const string LayerSubTabPrefsKey = "PJDev.UISystemSettings.LayerSubTab";

        public static UIViewCatalog LoadOrFindCatalog()
        {
            return LoadOrFind<UIViewCatalog>(CatalogPrefsKey);
        }

        public static UILayerSettings LoadOrFindLayerSettings()
        {
            return LoadOrFind<UILayerSettings>(LayerSettingsPrefsKey);
        }

        public static void Remember(UIViewCatalog catalog)
        {
            RememberAsset(CatalogPrefsKey, catalog);
        }

        public static void Remember(UILayerSettings layerSettings)
        {
            RememberAsset(LayerSettingsPrefsKey, layerSettings);
        }

        public static int CountAssets<T>() where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}").Length;
        }

        public static UIViewCatalog CreateCatalogAsset(string folder = null, bool promptForLocation = true)
        {
            if (!TryCreateAssetPath("Create UIView Catalog", "UIViewCatalog", folder, promptForLocation, out string path))
                return null;

            return CreateAssetAtPath<UIViewCatalog>(path);
        }

        public static UILayerSettings CreateLayerSettingsAsset(string folder = null, bool promptForLocation = true)
        {
            if (!TryCreateAssetPath("Create UI Layer Settings", "UILayerSettings", folder, promptForLocation, out string path))
                return null;

            UILayerSettings settings = CreateAssetAtPath<UILayerSettings>(path);
            if (settings == null)
                return null;

            settings.ResetToBuiltInDefaults();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static void SaveAssets(UIViewCatalog catalog, UILayerSettings layerSettings)
        {
            if (catalog != null)
                EditorUtility.SetDirty(catalog);

            if (layerSettings != null)
                EditorUtility.SetDirty(layerSettings);

            AssetDatabase.SaveAssets();
        }

        public static void Ping(UIViewCatalog catalog, UILayerSettings layerSettings)
        {
            Object target = catalog != null ? catalog : layerSettings;
            if (target != null)
                EditorGUIUtility.PingObject(target);
        }

        private static T LoadOrFind<T>(string prefsKey) where T : Object
        {
            string savedGuid = EditorPrefs.GetString(prefsKey, string.Empty);
            if (!string.IsNullOrEmpty(savedGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(savedGuid);
                T saved = AssetDatabase.LoadAssetAtPath<T>(path);
                if (saved != null)
                    return saved;
            }

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 1)
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        private static void RememberAsset(string prefsKey, Object asset)
        {
            if (asset == null)
            {
                EditorPrefs.DeleteKey(prefsKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return;

            EditorPrefs.SetString(prefsKey, AssetDatabase.AssetPathToGUID(path));
        }

        private static bool TryCreateAssetPath(
            string title,
            string defaultFileName,
            string folder,
            bool promptForLocation,
            out string assetPath)
        {
            assetPath = null;
            string directory = string.IsNullOrEmpty(folder) ? "Assets" : folder;

            if (promptForLocation)
            {
                return PJDevEditorAssetCreationUtility.TryPickAssetPath(
                    title,
                    directory,
                    defaultFileName,
                    PJDevEditorAssetCreationUtility.UISystemFolderPrefsKey,
                    out assetPath);
            }

            assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{defaultFileName}.asset");
            return true;
        }

        private static T CreateAssetAtPath<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            return asset;
        }
    }
}
