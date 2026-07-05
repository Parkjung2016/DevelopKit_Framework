using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    /// <summary>
    /// Unity Scene View Orientation Gizmo를 프리뷰에 표시합니다. (가능한 경우에만)
    /// </summary>
    internal sealed class MontageSceneViewBridge : IDisposable
    {
        private SceneView host;
        private object sceneViewRotation;
        private MethodInfo rotationOnGuiMethod;
        private MethodInfo rotationSkipFadingMethod;
        private MethodInfo createSceneCameraMethod;
        private PropertyInfo cameraViewportProperty;
        private bool isAvailable;

        public bool IsAvailable => isAvailable;

        public bool TryInitialize()
        {
            if (isAvailable)
                return true;

            if (host != null)
                return false;

            try
            {
                Assembly editorAssembly = typeof(SceneView).Assembly;
                Type rotationType = editorAssembly.GetType("UnityEditor.SceneViewRotation");
                if (rotationType == null)
                    return false;

                FieldInfo svRotField = typeof(SceneView).GetField("svRot", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo registerMethod = rotationType.GetMethod("Register", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                rotationOnGuiMethod = rotationType.GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                rotationSkipFadingMethod = rotationType.GetMethod("SkipFading", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                createSceneCameraMethod = typeof(SceneView).GetMethod("CreateSceneCameraAndLights", BindingFlags.Instance | BindingFlags.NonPublic);
                cameraViewportProperty = typeof(SceneView).GetProperty("cameraViewport", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (svRotField == null || registerMethod == null || rotationOnGuiMethod == null
                    || createSceneCameraMethod == null || cameraViewportProperty == null)
                {
                    return false;
                }

                host = ScriptableObject.CreateInstance<SceneView>();
                host.hideFlags = HideFlags.HideAndDontSave;
                createSceneCameraMethod.Invoke(host, null);

                sceneViewRotation = Activator.CreateInstance(rotationType);
                svRotField.SetValue(host, sceneViewRotation);
                registerMethod.Invoke(sceneViewRotation, new object[] { host });

                isAvailable = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Montage Scene Gizmo unavailable: {ex.Message}");
                isAvailable = false;
                return false;
            }
        }

        public void Prepare(MontageViewportCamera camera, Rect viewportRect, bool viewportActive)
        {
            if (!TryInitialize())
                return;

            camera.SyncToSceneView(host);
            host.in2DMode = camera.Is2DMode;
            cameraViewportProperty.SetValue(host, viewportRect, null);
        }

        public bool HandleOrientationGizmo(MontageViewportCamera camera)
        {
            if (!isAvailable || host.in2DMode)
                return false;

            Event evt = Event.current;
            int previousHotControl = GUIUtility.hotControl;
            rotationOnGuiMethod.Invoke(sceneViewRotation, new object[] { host });

            if (evt.type != EventType.Repaint)
                rotationSkipFadingMethod?.Invoke(sceneViewRotation, null);

            camera.SyncFromSceneView(host);
            return GUIUtility.hotControl != previousHotControl || evt.type == EventType.Used;
        }

        public void Dispose()
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
                host = null;
            }

            sceneViewRotation = null;
            isAvailable = false;
        }
    }
}
