using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageViewportCamera
    {
        public static readonly Quaternion DefaultRotation = Quaternion.Euler(15f, 25f, 0f);
        private static readonly Quaternion Mode2DRotation = Quaternion.identity;

        public Vector3 Pivot { get; set; }
        public Quaternion Rotation { get; set; } = DefaultRotation;
        public float Size { get; set; } = 10f;
        public bool Orthographic { get; set; }
        public bool Is2DMode { get; set; }
        public bool IsRotationLocked { get; set; }

        public void FrameBounds(Bounds bounds)
        {
            Pivot = bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            Size = Mathf.Max(1.5f, radius * 2.6f);
        }

        public void ApplyToPreviewCamera(Camera camera, bool modeTransitionActive = false)
        {
            if (modeTransitionActive)
            {
                camera.orthographic = Orthographic;
                camera.transform.position = Pivot + Rotation * new Vector3(0f, 0f, -Size);
                camera.transform.rotation = Rotation;

                if (Orthographic)
                {
                    camera.orthographicSize = Size * 0.5f;
                    camera.nearClipPlane = 0.01f;
                    camera.farClipPlane = Size * 20f + 10f;
                }
                else
                {
                    camera.nearClipPlane = Mathf.Max(0.01f, Size * 0.01f);
                    camera.farClipPlane = Size * 10f + 10f;
                }

                return;
            }

            if (Is2DMode)
            {
                camera.orthographic = true;
                camera.orthographicSize = Size * 0.5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = Size * 20f + 10f;
                camera.transform.SetPositionAndRotation(
                    Pivot + Vector3.back * Mathf.Max(10f, Size),
                    Mode2DRotation);
                return;
            }

            camera.orthographic = Orthographic;
            camera.transform.position = Pivot + Rotation * new Vector3(0f, 0f, -Size);
            camera.transform.rotation = Rotation;

            if (Orthographic)
            {
                camera.orthographicSize = Size * 0.5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = Size * 20f + 10f;
            }
            else
            {
                camera.nearClipPlane = Mathf.Max(0.01f, Size * 0.01f);
                camera.farClipPlane = Size * 10f + 10f;
            }
        }

        public void SyncFromSceneView(SceneView sceneView, bool syncViewMode = false)
        {
            Pivot = sceneView.pivot;
            Rotation = sceneView.rotation;
            Size = sceneView.size;
            Orthographic = sceneView.orthographic;

            if (syncViewMode)
            {
                Is2DMode = sceneView.in2DMode;
                IsRotationLocked = sceneView.isRotationLocked;
            }
        }

        public void SyncToSceneView(SceneView sceneView)
        {
            sceneView.pivot = Pivot;
            sceneView.rotation = Rotation;
            sceneView.size = Size;
            sceneView.orthographic = Orthographic;
            sceneView.in2DMode = Is2DMode;
            sceneView.isRotationLocked = IsRotationLocked;
            sceneView.LookAt(Pivot, Rotation, Size, Orthographic);
        }
    }
}
