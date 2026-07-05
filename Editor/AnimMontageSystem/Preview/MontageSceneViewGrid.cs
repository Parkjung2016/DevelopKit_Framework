using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageSceneViewGrid
    {
        private const float GridOpacity = 0.5f;
        private const float OrthographicAxisThreshold = 0.15f;

        private static readonly Color ViewGridColor = new(0.5f, 0.5f, 0.5f, 0.4f);

        private static bool initialized;
        private static bool isAvailable;
        private static Type drawGridParametersType;
        private static FieldInfo gridIdField;
        private static FieldInfo pivotField;
        private static FieldInfo rotationField;
        private static FieldInfo colorField;
        private static FieldInfo sizeField;
        private static MethodInfo drawCameraImplMethod;
        private static PropertyInfo gridSettingsInstanceProperty;
        private static PropertyInfo gridSizeProperty;
        private static PropertyInfo gridRotationProperty;
        private static PropertyInfo gridPositionProperty;

        public static void DrawPreview(Rect guiRect, Camera camera, MontageViewportCamera viewportCamera, bool useFrontGrid)
        {
            if (camera == null || viewportCamera == null)
                return;

            if (!TryInitialize())
            {
                camera.Render();
                return;
            }

            try
            {
                object parameters = CreateGridParameters(camera, useFrontGrid);
                drawCameraImplMethod.Invoke(
                    null,
                    new object[]
                    {
                        guiRect,
                        camera,
                        DrawCameraMode.Textured,
                        true,
                        parameters,
                        true,
                        false,
                        false,
                        null
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Montage Scene View grid unavailable: {ex.Message}");
                camera.Render();
            }
        }

        private static bool TryInitialize()
        {
            if (initialized)
                return isAvailable;

            initialized = true;
            try
            {
                Assembly editorAssembly = typeof(Handles).Assembly;
                drawGridParametersType = editorAssembly.GetType("UnityEditor.DrawGridParameters");
                Type gridSettingsType = editorAssembly.GetType("UnityEditor.GridSettings");

                drawCameraImplMethod = typeof(Handles).GetMethod(
                    "DrawCameraImpl",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(Rect),
                        typeof(Camera),
                        typeof(DrawCameraMode),
                        typeof(bool),
                        drawGridParametersType,
                        typeof(bool),
                        typeof(bool),
                        typeof(bool),
                        typeof(GameObject[])
                    },
                    null);

                if (drawGridParametersType == null || drawCameraImplMethod == null || gridSettingsType == null)
                    return false;

                BindingFlags fieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags propertyFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                gridIdField = drawGridParametersType.GetField("gridID", fieldFlags);
                pivotField = drawGridParametersType.GetField("pivot", fieldFlags);
                rotationField = drawGridParametersType.GetField("rotation", fieldFlags);
                colorField = drawGridParametersType.GetField("color", fieldFlags);
                sizeField = drawGridParametersType.GetField("size", fieldFlags);

                gridSettingsInstanceProperty = gridSettingsType.GetProperty("instance", propertyFlags);
                gridSizeProperty = gridSettingsType.GetProperty("gridSize", propertyFlags);
                gridRotationProperty = gridSettingsType.GetProperty("rotation", propertyFlags);
                gridPositionProperty = gridSettingsType.GetProperty("position", propertyFlags);

                isAvailable = gridIdField != null && pivotField != null && rotationField != null
                    && colorField != null && sizeField != null && gridSettingsInstanceProperty != null
                    && gridSizeProperty != null && gridRotationProperty != null && gridPositionProperty != null;
                return isAvailable;
            }
            catch
            {
                isAvailable = false;
                return false;
            }
        }

        private static object CreateGridParameters(Camera camera, bool useFrontGrid)
        {
            object settings = gridSettingsInstanceProperty.GetValue(null);
            Vector3 gridSize = (Vector3)gridSizeProperty.GetValue(settings);
            if (gridSize == Vector3.zero)
                gridSize = Vector3.one;

            Quaternion settingsRotation = (Quaternion)gridRotationProperty.GetValue(settings);
            Vector3 settingsPosition = (Vector3)gridPositionProperty.GetValue(settings);

            object parameters = Activator.CreateInstance(drawGridParametersType);
            Color color = ViewGridColor;
            color.a = GridOpacity;

            int gridId;
            Vector2 size;
            if (useFrontGrid || ShouldUseFrontGrid(camera))
            {
                gridId = 2;
                size = new Vector2(gridSize.x, gridSize.y);
            }
            else if (camera.orthographic && TryGetOrthographicGridId(camera, out int orthoGridId))
            {
                gridId = orthoGridId;
                size = GetGridSizeForAxis(orthoGridId, gridSize);
            }
            else
            {
                gridId = 1;
                size = GetGridSizeForAxis(1, gridSize);
            }

            gridIdField.SetValue(parameters, gridId);
            pivotField.SetValue(parameters, Quaternion.Inverse(settingsRotation) * settingsPosition);
            rotationField.SetValue(parameters, settingsRotation);
            colorField.SetValue(parameters, color);
            sizeField.SetValue(parameters, size);
            return parameters;
        }

        private static bool ShouldUseFrontGrid(Camera camera)
        {
            if (!camera.orthographic)
                return false;

            return Mathf.Abs(camera.transform.forward.z) >= 1f - OrthographicAxisThreshold;
        }

        private static bool TryGetOrthographicGridId(Camera camera, out int gridId)
        {
            Vector3 forward = camera.transform.forward;
            if (Mathf.Abs(forward.y) >= 1f - OrthographicAxisThreshold)
            {
                gridId = 1;
                return true;
            }

            if (Mathf.Abs(forward.x) >= 1f - OrthographicAxisThreshold)
            {
                gridId = 0;
                return true;
            }

            if (Mathf.Abs(forward.z) >= 1f - OrthographicAxisThreshold)
            {
                gridId = 2;
                return true;
            }

            gridId = 1;
            return false;
        }

        private static Vector2 GetGridSizeForAxis(int gridId, Vector3 gridSize) =>
            gridId switch
            {
                0 => new Vector2(gridSize.y, gridSize.z),
                2 => new Vector2(gridSize.x, gridSize.y),
                _ => new Vector2(gridSize.z, gridSize.x),
            };
    }
}
