using PJDev.UI;
using UnityEditor;
using UnityEngine;

namespace PJDev.UI.Editor
{
    internal static class SafeAreaGameObjectMenu
    {
        private const string MenuPath = "GameObject/PJDev/Safe Area";
        private const int MenuPriority = 2030;

        [MenuItem(MenuPath, false, MenuPriority)]
        private static void CreateSafeArea(MenuCommand menuCommand)
        {
            GameObject parent = menuCommand.context as GameObject;
            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea));
            GameObjectUtility.SetParentAndAlign(safeAreaObject, parent);

            RectTransform rect = safeAreaObject.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Undo.RegisterCreatedObjectUndo(safeAreaObject, "Create Safe Area");
            Selection.activeGameObject = safeAreaObject;

            safeAreaObject.GetComponent<SafeArea>().Refresh();
        }

        [MenuItem(MenuPath, true, MenuPriority)]
        private static bool ValidateCreateSafeArea()
        {
            if (Selection.activeTransform == null)
                return true;

            return Selection.activeTransform.GetComponentInParent<Canvas>() != null;
        }
    }
}
