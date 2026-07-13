using System;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageSceneViewNavigation
    {
        private const string GridHalfSizePrefKey = "PJDev.AnimMontage.Editor.PreviewGridHalfSize";
        private const string GridStepPrefKey = "PJDev.AnimMontage.Editor.PreviewGridStep";
        private const string LightEnabledPrefKey = "PJDev.AnimMontage.Editor.PreviewLightEnabled";
        private const string LightIntensityPrefKey = "PJDev.AnimMontage.Editor.PreviewLightIntensity";
        private const string LightAmbientPrefKey = "PJDev.AnimMontage.Editor.PreviewLightAmbient";
        private const string LightYawPrefKey = "PJDev.AnimMontage.Editor.PreviewLightYaw";
        private const string LightPitchPrefKey = "PJDev.AnimMontage.Editor.PreviewLightPitch";
        private const string LightShadowPrefKey = "PJDev.AnimMontage.Editor.PreviewLightShadow";
        private const string SkyboxEnabledPrefKey = "PJDev.AnimMontage.Editor.PreviewSkyboxEnabled";
        private const string SkyboxMaterialPathPrefKey = "PJDev.AnimMontage.Editor.PreviewSkyboxMaterialPath";
        private const string PlaneColorPrefKey = "PJDev.AnimMontage.Editor.PreviewPlaneColor";
        private const string SkyTintPrefKey = "PJDev.AnimMontage.Editor.PreviewSkyTint";
        private const string SkyGroundColorPrefKey = "PJDev.AnimMontage.Editor.PreviewSkyGroundColor";
        private const string SkyAtmospherePrefKey = "PJDev.AnimMontage.Editor.PreviewSkyAtmosphere";
        private const string SkyExposurePrefKey = "PJDev.AnimMontage.Editor.PreviewSkyExposure";
        private const float DefaultGridHalfSize = 5f;
        private const float DefaultGridStep = 0.5f;
        private const float MinGridHalfSize = 1f;
        private const float MaxGridHalfSize = 100f;
        private const float MinGridStep = 0.05f;
        private const float MaxGridStep = 10f;
        private const float DefaultLightIntensity = 1.1f;
        private const float DefaultLightAmbient = 0.2f;
        private const float DefaultLightYaw = 40f;
        private const float DefaultLightPitch = 40f;
        private const float DefaultSkyAtmosphere = 1f;
        private const float DefaultSkyExposure = 1.3f;
        private static readonly Color DefaultPlaneColor = new(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Color DefaultSkyTint = new(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color DefaultSkyGroundColor = new(0.369f, 0.349f, 0.341f, 1f);

        private static readonly GUIContent Mode2DContent = EditorGUIUtility.IconContent("SceneView2D", "Toggle 2D mode");
        private static readonly MontageViewportModeTransition ModeTransition = new();
        private static bool showGridSettings;
        private static bool showLightSettings;

        public static bool IsModeTransitionActive => ModeTransition.IsActive;

        public static bool ModeTransitionTargetIs2D => ModeTransition.TargetIs2D;

        public static float GridHalfSize => Mathf.Clamp(EditorPrefs.GetFloat(GridHalfSizePrefKey, DefaultGridHalfSize), MinGridHalfSize, MaxGridHalfSize);

        public static float GridStep => Mathf.Clamp(EditorPrefs.GetFloat(GridStepPrefKey, DefaultGridStep), MinGridStep, MaxGridStep);

        public static bool LightEnabled => EditorPrefs.GetBool(LightEnabledPrefKey, true);

        public static float LightIntensity => Mathf.Clamp(EditorPrefs.GetFloat(LightIntensityPrefKey, DefaultLightIntensity), 0f, 4f);

        public static float LightAmbient => Mathf.Clamp(EditorPrefs.GetFloat(LightAmbientPrefKey, DefaultLightAmbient), 0f, 1f);

        public static float LightYaw => EditorPrefs.GetFloat(LightYawPrefKey, DefaultLightYaw);

        public static float LightPitch => Mathf.Clamp(EditorPrefs.GetFloat(LightPitchPrefKey, DefaultLightPitch), -80f, 80f);

        public static bool LightShadows => EditorPrefs.GetBool(LightShadowPrefKey, true);

        public static bool SkyboxEnabled => EditorPrefs.GetBool(SkyboxEnabledPrefKey, false);

        public static Material SkyboxMaterial => LoadSkyboxMaterial();

        public static Color PlaneColor => GetColor(PlaneColorPrefKey, DefaultPlaneColor);

        public static Color SkyTint => GetColor(SkyTintPrefKey, DefaultSkyTint);

        public static Color SkyGroundColor => GetColor(SkyGroundColorPrefKey, DefaultSkyGroundColor);

        public static float SkyAtmosphere => Mathf.Clamp(EditorPrefs.GetFloat(SkyAtmospherePrefKey, DefaultSkyAtmosphere), 0f, 5f);

        public static float SkyExposure => Mathf.Clamp(EditorPrefs.GetFloat(SkyExposurePrefKey, DefaultSkyExposure), 0f, 8f);

        public static Quaternion LightRotation => Quaternion.Euler(LightPitch, LightYaw, 0f);

        public static bool ShouldUseFrontGrid(MontageViewportCamera camera) =>
            camera.Is2DMode || (ModeTransition.IsActive && ModeTransition.TargetIs2D);

        public static void ApplyModeTransition(MontageViewportCamera camera) =>
            ModeTransition.Apply(camera);

        public static Rect GetToolbarRect(Rect viewportRect)
        {
            float height = 22f;
            if (showGridSettings)
                height += 82f;

            if (showLightSettings)
                height += 336f;

            float width = Mathf.Min(390f, Mathf.Max(260f, viewportRect.width - 8f));
            return new Rect(viewportRect.x + 4f, viewportRect.y + 4f, width, height);
        }

        public static bool DrawToolbar(Rect viewportRect, MontageViewportCamera camera, Action requestRepaint)
        {
            ApplyModeTransition(camera);

            Rect toggleRect = new(viewportRect.x + 4f, viewportRect.y + 4f, 28f, 22f);
            Rect gridRect = new(toggleRect.xMax + 4f, toggleRect.y, 50f, 22f);
            Rect lightRect = new(gridRect.xMax + 4f, toggleRect.y, 50f, 22f);
            Rect skyboxRect = new(lightRect.xMax + 4f, toggleRect.y, 58f, 22f);
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

            if (GUI.Button(lightRect, "Light", EditorStyles.toolbarButton))
            {
                showLightSettings = !showLightSettings;
                changed = true;
            }

            EditorGUI.BeginChangeCheck();
            bool skybox = GUI.Toggle(skyboxRect, SkyboxEnabled, "Skybox", EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(SkyboxEnabledPrefKey, skybox);
                changed = true;
            }

            float panelY = toggleRect.yMax + 4f;
            if (showGridSettings)
            {
                changed |= DrawGridSettings(new Rect(toggleRect.x, panelY, 300f, 78f));
                panelY += 82f;
            }

            if (showLightSettings)
                changed |= DrawLightSettings(new Rect(toggleRect.x, panelY, 376f, 332f));

            if (changed)
                requestRepaint?.Invoke();

            return changed;
        }

        private static bool DrawGridSettings(Rect rect)
        {
            DrawSettingsPanelBackground(rect);

            EditorGUI.BeginChangeCheck();
            float nextHalfSize = DrawFloatField(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 20f), "Range", GridHalfSize, MinGridHalfSize, MaxGridHalfSize);
            float nextStep = DrawFloatField(new Rect(rect.x + 10f, rect.y + 36f, rect.width - 20f, 20f), "Step", GridStep, MinGridStep, MaxGridStep);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                EditorPrefs.SetFloat(GridHalfSizePrefKey, nextHalfSize);
                EditorPrefs.SetFloat(GridStepPrefKey, nextStep);
            }

            if (GUI.Button(new Rect(rect.x + rect.width - 58f, rect.y + rect.height - 22f, 50f, 18f), "Reset", EditorStyles.miniButton))
            {
                EditorPrefs.SetFloat(GridHalfSizePrefKey, DefaultGridHalfSize);
                EditorPrefs.SetFloat(GridStepPrefKey, DefaultGridStep);
                changed = true;
            }

            return changed;
        }
        private static bool DrawLightSettings(Rect rect)
        {
            DrawSettingsPanelBackground(rect);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUI.ToggleLeft(new Rect(rect.x + 10f, rect.y + 8f, 78f, 20f), "Enabled", LightEnabled);
            bool shadows = EditorGUI.ToggleLeft(new Rect(rect.x + 104f, rect.y + 8f, 82f, 20f), "Shadow", LightShadows);
            float intensity = DrawFloatField(new Rect(rect.x + 10f, rect.y + 36f, rect.width - 20f, 20f), "Intensity", LightIntensity, 0f, 4f);
            float ambient = DrawFloatField(new Rect(rect.x + 10f, rect.y + 64f, rect.width - 20f, 20f), "Ambient", LightAmbient, 0f, 1f);
            float yaw = DrawFloatField(new Rect(rect.x + 10f, rect.y + 92f, rect.width - 20f, 20f), "Yaw", LightYaw, -180f, 180f);
            float pitch = DrawFloatField(new Rect(rect.x + 10f, rect.y + 120f, rect.width - 20f, 20f), "Pitch", LightPitch, -80f, 80f);
            Color planeColor = EditorGUI.ColorField(new Rect(rect.x + 10f, rect.y + 152f, rect.width - 20f, 20f), "Plane", PlaneColor);
            Material skyboxMaterial = (Material)EditorGUI.ObjectField(new Rect(rect.x + 10f, rect.y + 180f, rect.width - 20f, 20f), "Sky Mat", SkyboxMaterial, typeof(Material), false);
            Color skyTint = EditorGUI.ColorField(new Rect(rect.x + 10f, rect.y + 208f, rect.width - 20f, 20f), "Sky", SkyTint);
            Color groundColor = EditorGUI.ColorField(new Rect(rect.x + 10f, rect.y + 236f, rect.width - 20f, 20f), "Ground", SkyGroundColor);
            float atmosphere = DrawFloatField(new Rect(rect.x + 10f, rect.y + 264f, rect.width - 20f, 20f), "Atmos", SkyAtmosphere, 0f, 5f);
            float exposure = DrawFloatField(new Rect(rect.x + 10f, rect.y + 292f, rect.width - 84f, 20f), "Exposure", SkyExposure, 0f, 8f);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                EditorPrefs.SetBool(LightEnabledPrefKey, enabled);
                EditorPrefs.SetBool(LightShadowPrefKey, shadows);
                EditorPrefs.SetFloat(LightIntensityPrefKey, intensity);
                EditorPrefs.SetFloat(LightAmbientPrefKey, ambient);
                EditorPrefs.SetFloat(LightYawPrefKey, yaw);
                EditorPrefs.SetFloat(LightPitchPrefKey, pitch);
                SetColor(PlaneColorPrefKey, planeColor);
                SetSkyboxMaterial(skyboxMaterial);
                SetColor(SkyTintPrefKey, skyTint);
                SetColor(SkyGroundColorPrefKey, groundColor);
                EditorPrefs.SetFloat(SkyAtmospherePrefKey, atmosphere);
                EditorPrefs.SetFloat(SkyExposurePrefKey, exposure);
            }

            if (GUI.Button(new Rect(rect.x + rect.width - 58f, rect.y + rect.height - 22f, 50f, 18f), "Reset", EditorStyles.miniButton))
            {
                EditorPrefs.SetBool(LightEnabledPrefKey, true);
                EditorPrefs.SetBool(LightShadowPrefKey, true);
                EditorPrefs.SetFloat(LightIntensityPrefKey, DefaultLightIntensity);
                EditorPrefs.SetFloat(LightAmbientPrefKey, DefaultLightAmbient);
                EditorPrefs.SetFloat(LightYawPrefKey, DefaultLightYaw);
                EditorPrefs.SetFloat(LightPitchPrefKey, DefaultLightPitch);
                EditorPrefs.DeleteKey(SkyboxMaterialPathPrefKey);
                SetColor(PlaneColorPrefKey, DefaultPlaneColor);
                SetColor(SkyTintPrefKey, DefaultSkyTint);
                SetColor(SkyGroundColorPrefKey, DefaultSkyGroundColor);
                EditorPrefs.SetFloat(SkyAtmospherePrefKey, DefaultSkyAtmosphere);
                EditorPrefs.SetFloat(SkyExposurePrefKey, DefaultSkyExposure);
                ReleaseEditorGuiFocus();
                changed = true;
            }

            return changed;
        }

        private static float DrawFloatField(Rect rect, string label, float value, float min, float max)
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            float previousFieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.labelWidth = 74f;
            EditorGUIUtility.fieldWidth = 78f;

            float nextValue = EditorGUI.Slider(rect, label, value, min, max);

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUIUtility.fieldWidth = previousFieldWidth;
            return Mathf.Clamp(nextValue, min, max);
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

        public static void Shutdown()
        {
            showGridSettings = false;
            showLightSettings = false;
            ModeTransition.Shutdown();
        }

        private static void DrawSettingsPanelBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.09f, 0.09f, 0.09f, 0.96f));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        }

        private static void ReleaseEditorGuiFocus()
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            EditorGUI.FocusTextInControl(null);
            MontageViewportInput.CancelInteraction();
            EditorApplication.delayCall += MontageViewportInput.CancelInteraction;
        }
        private static Material LoadSkyboxMaterial()
        {
            string path = EditorPrefs.GetString(SkyboxMaterialPathPrefKey, string.Empty);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void SetSkyboxMaterial(Material material)
        {
            if (material == null)
            {
                EditorPrefs.DeleteKey(SkyboxMaterialPathPrefKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path))
                EditorPrefs.DeleteKey(SkyboxMaterialPathPrefKey);
            else
                EditorPrefs.SetString(SkyboxMaterialPathPrefKey, path);
        }

        private static Color GetColor(string key, Color fallback)
        {
            string html = EditorPrefs.GetString(key, string.Empty);
            return ColorUtility.TryParseHtmlString("#" + html, out Color color) ? color : fallback;
        }

        private static void SetColor(string key, Color color) =>
            EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(color));
    }
}