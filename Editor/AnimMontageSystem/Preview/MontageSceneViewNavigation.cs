using System;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageSceneViewNavigation
    {
        private const string GridHalfSizePrefKey = "PJDev.AnimMontage.Editor.PreviewGridHalfSize";
        private const string GridStepPrefKey = "PJDev.AnimMontage.Editor.PreviewGridStep";
        private const float DefaultGridHalfSize = 5f;
        private const float DefaultGridStep = 0.5f;
        private const float MinGridHalfSize = 1f;
        private const float MaxGridHalfSize = 100f;
        private const float MinGridStep = 0.05f;
        private const float MaxGridStep = 10f;

        private static readonly GUIContent Mode2DContent = EditorGUIUtility.IconContent("SceneView2D", "Toggle 2D mode");
        private static readonly MontageViewportModeTransition ModeTransition = new();
        private static bool showGridSettings;

        public static bool IsModeTransitionActive => ModeTransition.IsActive;

        public static bool ModeTransitionTargetIs2D => ModeTransition.TargetIs2D;

        public static float GridHalfSize => Mathf.Clamp(EditorPrefs.GetFloat(GridHalfSizePrefKey, DefaultGridHalfSize), MinGridHalfSize, MaxGridHalfSize);

        public static float GridStep => Mathf.Clamp(EditorPrefs.GetFloat(GridStepPrefKey, DefaultGridStep), MinGridStep, MaxGridStep);

        public static bool ShouldUseFrontGrid(MontageViewportCamera camera) =>
            camera.Is2DMode || (ModeTransition.IsActive && ModeTransition.TargetIs2D);

        public static void ApplyModeTransition(MontageViewportCamera camera) =>
            ModeTransition.Apply(camera);

        public static Rect GetToolbarRect(Rect viewportRect) =>
            new(viewportRect.x + 4f, viewportRect.y + 4f, showGridSettings ? 180f : 82f, showGridSettings ? 96f : 22f);

        public static bool DrawToolbar(Rect viewportRect, MontageViewportCamera camera, Action requestRepaint)
        {
            ApplyModeTransition(camera);

            Rect toggleRect = new(viewportRect.x + 4f, viewportRect.y + 4f, 28f, 22f);
            Rect gridRect = new(toggleRect.xMax + 4f, toggleRect.y, 50f, 22f);
            bool displayed2D = camera.Is2DMode || (ModeTransition.IsActive && ModeTransition.TargetIs2D);
            bool changed = false;

            EditorGUI.BeginChangeCheck();
            bool next2D = GUI.Toggle(toggleRect, displayed2D, Mode2DContent, EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                if (!ModeTransition.IsActive)
                    ModeTransition.Begin(camera, next2D, requestRepaint);

                changed = true;
            }

            if (GUI.Button(gridRect, "Grid", EditorStyles.toolbarButton))
            {
                showGridSettings = !showGridSettings;
                changed = true;
            }

            if (showGridSettings)
                changed |= DrawGridSettings(new Rect(toggleRect.x, toggleRect.yMax + 4f, 172f, 66f));

            if (changed)
                requestRepaint?.Invoke();

            return changed;
        }

        private static bool DrawGridSettings(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 46f;
            EditorGUI.BeginChangeCheck();
            float nextHalfSize = EditorGUI.FloatField(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 18f), "Range", GridHalfSize);
            float nextStep = EditorGUI.FloatField(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, 18f), "Step", GridStep);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = oldLabelWidth;
            if (changed)
            {
                EditorPrefs.SetFloat(GridHalfSizePrefKey, Mathf.Clamp(nextHalfSize, MinGridHalfSize, MaxGridHalfSize));
                EditorPrefs.SetFloat(GridStepPrefKey, Mathf.Clamp(nextStep, MinGridStep, MaxGridStep));
            }

            if (GUI.Button(new Rect(rect.x + rect.width - 58f, rect.y + rect.height - 22f, 50f, 18f), "Reset", EditorStyles.miniButton))
            {
                EditorPrefs.SetFloat(GridHalfSizePrefKey, DefaultGridHalfSize);
                EditorPrefs.SetFloat(GridStepPrefKey, DefaultGridStep);
                changed = true;
            }

            return changed;
        }

        public static bool Toggle2DMode(MontageViewportCamera camera, Action requestRepaint)
        {
            if (camera == null || ModeTransition.IsActive)
                return false;

            ModeTransition.Begin(camera, !camera.Is2DMode, requestRepaint);
            requestRepaint?.Invoke();
            return true;
        }

        public static bool IsToolbarRect(Rect viewportRect, Vector2 mousePosition) =>
            GetToolbarRect(viewportRect).Contains(mousePosition);

        public static void Shutdown() => ModeTransition.Shutdown();
    }
}
