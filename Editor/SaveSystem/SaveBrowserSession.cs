using PJDev.DevelopKit.Framework.SaveSystem.Runtime;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.SaveSystem
{
    internal static class SaveBrowserSession
    {
        private const string LastSettingsGuidKey =
            "PJDev.SaveSystem.SlotBrowser.LastSettingsGuid";

        public static void SaveLastSettings(SaveSettingsSO settings)
        {
            if (settings == null)
            {
                EditorPrefs.DeleteKey(LastSettingsGuidKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(settings);
            if (string.IsNullOrEmpty(path))
            {
                EditorPrefs.DeleteKey(LastSettingsGuidKey);
                return;
            }

            EditorPrefs.SetString(
                LastSettingsGuidKey,
                AssetDatabase.AssetPathToGUID(path));
        }

        public static SaveSettingsSO LoadLastSettings()
        {
            string guid = EditorPrefs.GetString(LastSettingsGuidKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                EditorPrefs.DeleteKey(LastSettingsGuidKey);
                return null;
            }

            SaveSettingsSO settings = AssetDatabase.LoadAssetAtPath<SaveSettingsSO>(path);
            if (settings == null)
                EditorPrefs.DeleteKey(LastSettingsGuidKey);

            return settings;
        }
    }
}