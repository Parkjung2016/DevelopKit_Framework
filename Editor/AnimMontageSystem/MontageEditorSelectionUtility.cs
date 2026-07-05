using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageEditorSelectionUtility
    {
        public static void SanitizeForDomainReload(Object preferredSelection = null)
        {
            if (preferredSelection != null && IsSafePersistentSelection(preferredSelection))
            {
                Selection.activeObject = preferredSelection;
                return;
            }

            RemoveDestroyedObjectsFromSelection();
            RemoveNonPersistentSceneObjectsFromSelection();
            RemoveDestroyedObjectsFromSelection();
        }

        public static void RemoveDestroyedObjectsFromSelection()
        {
            Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
                return;

            List<Object> filtered = new(selected.Length);
            for (int i = 0; i < selected.Length; i++)
            {
                if (IsAlive(selected[i]))
                    filtered.Add(selected[i]);
            }

            if (filtered.Count == selected.Length)
                return;

            Selection.objects = filtered.Count == 0 ? System.Array.Empty<Object>() : filtered.ToArray();
        }

        public static void RemoveNonPersistentSceneObjectsFromSelection()
        {
            Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
                return;

            List<Object> filtered = new(selected.Length);
            for (int i = 0; i < selected.Length; i++)
            {
                if (IsSafePersistentSelection(selected[i]))
                    filtered.Add(selected[i]);
            }

            if (filtered.Count == selected.Length)
                return;

            Selection.objects = filtered.Count == 0 ? System.Array.Empty<Object>() : filtered.ToArray();
        }

        public static void RemoveObjectFromSelection(Object target)
        {
            if (!IsAlive(target))
                return;

            Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
                return;

            List<Object> filtered = new(selected.Length);
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] != target)
                    filtered.Add(selected[i]);
            }

            if (filtered.Count == selected.Length)
                return;

            Selection.objects = filtered.Count == 0 ? System.Array.Empty<Object>() : filtered.ToArray();
        }

        public static void RemoveHierarchyFromSelection(GameObject root)
        {
            if (!IsAlive(root))
                return;

            Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
                return;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            List<Object> filtered = new(selected.Length);

            for (int i = 0; i < selected.Length; i++)
            {
                Object item = selected[i];
                if (!IsAlive(item))
                    continue;

                if (BelongsToHierarchy(item, transforms))
                    continue;

                filtered.Add(item);
            }

            if (filtered.Count == selected.Length)
                return;

            Selection.objects = filtered.Count == 0 ? System.Array.Empty<Object>() : filtered.ToArray();
        }

        private static bool BelongsToHierarchy(Object item, Transform[] transforms)
        {
            for (int t = 0; t < transforms.Length; t++)
            {
                Transform transform = transforms[t];
                if (!IsAlive(transform))
                    continue;

                if (item == transform.gameObject || item == transform)
                    return true;
            }

            return false;
        }

        private static bool IsAlive(Object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;

            return obj;
        }

        private static bool IsSafePersistentSelection(Object obj)
        {
            if (!IsAlive(obj))
                return false;

            if (obj is GameObject go)
                return EditorUtility.IsPersistent(go);

            if (obj is Component component)
            {
                if (!IsAlive(component))
                    return false;

                return EditorUtility.IsPersistent(component.gameObject);
            }

            return EditorUtility.IsPersistent(obj);
        }
    }
}
