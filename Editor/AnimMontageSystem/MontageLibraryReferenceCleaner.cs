using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageLibraryReferenceCleaner
    {
        public static bool RemoveMissingMontageReferences()
        {
            return RemoveMontageReferences(null);
        }

        public static bool RemoveMontageReferences(AnimMontageSO montage)
        {
            bool anyChanged = false;
            string[] libraryGuids = AssetDatabase.FindAssets($"t:{nameof(AnimMontageLibrarySO)}");
            for (int i = 0; i < libraryGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(libraryGuids[i]);
                AnimMontageLibrarySO library = AssetDatabase.LoadAssetAtPath<AnimMontageLibrarySO>(path);
                if (library == null)
                    continue;

                SerializedObject so = new(library);
                SerializedProperty montages = so.FindProperty("montages");
                if (montages == null || !montages.isArray)
                    continue;

                bool changed = false;
                for (int index = montages.arraySize - 1; index >= 0; index--)
                {
                    SerializedProperty element = montages.GetArrayElementAtIndex(index);
                    Object value = element.objectReferenceValue;
                    if (value != null && value != montage)
                        continue;

                    RemoveArrayElementAt(montages, index);
                    changed = true;
                }

                if (!changed)
                    continue;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(library);
                anyChanged = true;
            }

            if (anyChanged)
                AssetDatabase.SaveAssets();

            return anyChanged;
        }

        private static void RemoveArrayElementAt(SerializedProperty array, int index)
        {
            int size = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == size)
                array.DeleteArrayElementAtIndex(index);
        }
    }
}