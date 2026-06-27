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

        public static UIViewCatalog CreateCatalogAsset(string folder = null)
        {
            return CreateAsset<UIViewCatalog>("UIViewCatalog", folder);
        }

        public static UILayerSettings CreateLayerSettingsAsset(string folder = null)
        {
            UILayerSettings settings = CreateAsset<UILayerSettings>("UILayerSettings", folder);
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

        private static T CreateAsset<T>(string defaultName, string folder) where T : ScriptableObject
        {
            string directory = string.IsNullOrEmpty(folder) ? "Assets" : folder;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{defaultName}.asset");
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            return asset;
        }
    }
}
