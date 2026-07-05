using System;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageSceneViewNavigation
    {
        private static readonly GUIContent Mode2DContent = EditorGUIUtility.IconContent("SceneView2D", "Toggle 2D mode");
        private static readonly MontageViewportModeTransition ModeTransition = new();

        public static bool IsModeTransitionActive => ModeTransition.IsActive;

        public static bool ModeTransitionTargetIs2D => ModeTransition.TargetIs2D;

        public static bool ShouldUseFrontGrid(MontageViewportCamera camera) =>
            camera.Is2DMode || (ModeTransition.IsActive && ModeTransition.TargetIs2D);

        public static void ApplyModeTransition(MontageViewportCamera camera) =>
            ModeTransition.Apply(camera);

        public static Rect GetToolbarRect(Rect viewportRect) =>
            new(viewportRect.x + 4f, viewportRect.y + 4f, 28f, 22f);

        public static bool DrawToolbar(Rect viewportRect, MontageViewportCamera camera, Action requestRepaint)
        {
            ApplyModeTransition(camera);

            Rect toggleRect = GetToolbarRect(viewportRect);
            bool displayed2D = camera.Is2DMode || (ModeTransition.IsActive && ModeTransition.TargetIs2D);

            EditorGUI.BeginChangeCheck();
            bool next2D = GUI.Toggle(toggleRect, displayed2D, Mode2DContent, EditorStyles.toolbarButton);
            if (!EditorGUI.EndChangeCheck())
                return false;

            if (!ModeTransition.IsActive)
                ModeTransition.Begin(camera, next2D, requestRepaint);

            requestRepaint?.Invoke();
            return true;
        }

        public static bool IsToolbarRect(Rect viewportRect, Vector2 mousePosition) =>
            GetToolbarRect(viewportRect).Contains(mousePosition);

        public static void Shutdown() => ModeTransition.Shutdown();
    }
}
