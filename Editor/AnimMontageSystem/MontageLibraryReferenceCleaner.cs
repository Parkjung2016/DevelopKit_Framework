using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageLibraryReferenceCleaner
    {
        public static bool RemoveMissingAnimationReferences()
        {
            bool changed = false;
            string[] libraryGuids = AssetDatabase.FindAssets($"t:{nameof(AnimMontageLibrarySO)}");
            for (int i = 0; i < libraryGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(libraryGuids[i]);
                AnimMontageLibrarySO library = AssetDatabase.LoadAssetAtPath<AnimMontageLibrarySO>(path);
                if (library == null)
                    continue;

                SerializedObject serialized = new(library);
                bool libraryChanged = RemoveMissing(serialized.FindProperty("sequences"));
                libraryChanged |= RemoveMissing(serialized.FindProperty("montages"));
                if (!libraryChanged)
                    continue;

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(library);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();

            return changed;
        }

        public static bool RemoveMissingMontageReferences() => RemoveMissingAnimationReferences();

        public static bool RemoveMontageReferences(AnimMontageSO montage)
        {
            if (montage == null)
                return RemoveMissingAnimationReferences();

            return RemoveReference("montages", montage);
        }

        public static bool RemoveSequenceReferences(AnimSequenceSO sequence)
        {
            if (sequence == null)
                return RemoveMissingAnimationReferences();

            return RemoveReference("sequences", sequence);
        }

        private static bool RemoveReference(string propertyName, Object target)
        {
            bool changed = false;
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(AnimMontageLibrarySO)}");
            for (int i = 0; i < guids.Length; i++)
            {
                AnimMontageLibrarySO library = AssetDatabase.LoadAssetAtPath<AnimMontageLibrarySO>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (library == null)
                    continue;

                SerializedObject serialized = new(library);
                SerializedProperty assets = serialized.FindProperty(propertyName);
                if (!RemoveMatching(assets, target))
                    continue;

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(library);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();

            return changed;
        }

        private static bool RemoveMissing(SerializedProperty assets) => RemoveMatching(assets, null);

        private static bool RemoveMatching(SerializedProperty assets, Object target)
        {
            if (assets == null || !assets.isArray)
                return false;

            bool changed = false;
            for (int index = assets.arraySize - 1; index >= 0; index--)
            {
                Object value = assets.GetArrayElementAtIndex(index).objectReferenceValue;
                if (target == null ? value != null : value != target)
                    continue;

                RemoveArrayElementAt(assets, index);
                changed = true;
            }

            return changed;
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