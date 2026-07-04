using PJDev.DevelopKit.Editors;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.SocketSystem
{
    internal static class ObjectSocketGameObjectMenu
    {
        private const string MenuPath = "GameObject/PJDev/Object Socket";
        private const int MenuPriority = PJDevMenuPriority.GameObjectRoot;

        [MenuItem(MenuPath, false, MenuPriority)]
        private static void CreateObjectSocket(MenuCommand menuCommand)
        {
            GameObject parent = menuCommand.context as GameObject;
            var socketObject = new GameObject("socket", typeof(ObjectSocket));
            GameObjectUtility.SetParentAndAlign(socketObject, parent);

            Undo.RegisterCreatedObjectUndo(socketObject, "Create Object Socket");
            Selection.activeGameObject = socketObject;
        }
    }
}
