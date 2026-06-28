using System;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal static class UISystemEditorReferencePropagation
    {
        public static int PropagateCanvasGroupIdRename(UILayerSettings settings, string oldGroupId, string newGroupId)
        {
            if (settings == null ||
                string.IsNullOrEmpty(oldGroupId) ||
                string.IsNullOrEmpty(newGroupId) ||
                string.Equals(oldGroupId, newGroupId, StringComparison.Ordinal))
            {
                return 0;
            }

            SerializedObject serializedObject = new(settings);
            SerializedProperty layers = serializedObject.FindProperty("layers");
            if (layers == null)
                return 0;

            Undo.RecordObject(settings, "Rename Canvas Group ID");

            int updated = 0;
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                SerializedProperty canvasGroupIdProp = element.FindPropertyRelative("canvasGroupId");
                if (canvasGroupIdProp == null)
                    continue;

                string currentGroupId = canvasGroupIdProp.stringValue;
                if (string.IsNullOrEmpty(currentGroupId))
                    currentGroupId = UISystemEditorCanvasGroups.ReadLegacyCanvasGroupId(canvasGroupIdProp);

                if (!string.Equals(currentGroupId, oldGroupId, StringComparison.Ordinal))
                    continue;

                canvasGroupIdProp.stringValue = newGroupId;
                updated++;
            }

            if (updated > 0)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
            }

            return updated;
        }

        public static int PropagateLayerIdRename(UILayerSettings settings, string oldLayerId, string newLayerId)
        {
            if (string.IsNullOrEmpty(oldLayerId) ||
                string.IsNullOrEmpty(newLayerId) ||
                string.Equals(oldLayerId, newLayerId, StringComparison.Ordinal))
            {
                return 0;
            }

            int updated = PropagateLayerIdRenameInSettings(settings, oldLayerId, newLayerId);
            updated += PropagateLayerIdRenameInViewPrefabs(oldLayerId, newLayerId);
            return updated;
        }

        private static int PropagateLayerIdRenameInSettings(UILayerSettings settings, string oldLayerId, string newLayerId)
        {
            if (settings == null)
                return 0;

            SerializedObject serializedObject = new(settings);
            SerializedProperty layers = serializedObject.FindProperty("layers");
            if (layers == null)
                return 0;

            Undo.RecordObject(settings, "Rename UI Layer ID");

            int updated = 0;
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                SerializedProperty rootNameProp = element.FindPropertyRelative("rootName");
                if (rootNameProp == null ||
                    !string.Equals(rootNameProp.stringValue, oldLayerId, StringComparison.Ordinal))
                {
                    continue;
                }

                rootNameProp.stringValue = newLayerId;
                updated++;
            }

            if (updated > 0)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
            }

            return updated;
        }

        private static int PropagateLayerIdRenameInViewPrefabs(string oldLayerId, string newLayerId)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int updated = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                UIViewBase[] views = prefabRoot.GetComponentsInChildren<UIViewBase>(true);
                for (int j = 0; j < views.Length; j++)
                {
                    UIViewBase view = views[j];
                    if (view == null)
                        continue;

                    SerializedObject serializedObject = new(view);
                    SerializedProperty layerIdProp = serializedObject.FindProperty("layerId");
                    if (layerIdProp == null ||
                        !string.Equals(layerIdProp.stringValue, oldLayerId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Undo.RecordObject(view, "Rename UI Layer ID");
                    layerIdProp.stringValue = newLayerId;
                    serializedObject.ApplyModifiedProperties();
                    UISystemEditorLayers.PersistLayerIdProperty(layerIdProp);
                    updated++;
                }
            }

            return updated;
        }
    }
}
