using System;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageViewportModeTransition
    {
        private const float Duration = 0.25f;
        private static readonly Quaternion Mode2DRotation = Quaternion.identity;

        private bool active;
        private double startTime;
        private Quaternion fromRotation;
        private Quaternion toRotation;
        private float fromSize;
        private float toSize;
        private bool fromOrthographic;
        private bool toOrthographic;
        private bool target2D;
        private bool hasSaved3DState;
        private bool hasSaved2DState;
        private Quaternion saved3DRotation;
        private bool saved3DOrthographic;
        private float saved3DSize;
        private float saved2DSize;
        private Action repaintCallback;

        public bool IsActive => active;

        public bool TargetIs2D => target2D;

        public void Begin(MontageViewportCamera camera, bool enable2D, Action requestRepaint)
        {
            if (camera == null)
                return;

            if (enable2D == camera.Is2DMode)
                return;

            if (enable2D)
            {
                saved3DRotation = camera.Rotation;
                saved3DOrthographic = camera.Orthographic;
                saved3DSize = camera.Size;
                hasSaved3DState = true;
                fromRotation = camera.Rotation;
                toRotation = Mode2DRotation;
                fromSize = camera.Size;
                toSize = hasSaved2DState ? saved2DSize : camera.Size;
                fromOrthographic = camera.Orthographic;
                toOrthographic = true;
            }
            else
            {
                saved2DSize = camera.Size;
                hasSaved2DState = true;
                fromRotation = Mode2DRotation;
                toRotation = hasSaved3DState ? saved3DRotation : MontageViewportCamera.DefaultRotation;
                fromSize = camera.Size;
                toSize = hasSaved3DState ? saved3DSize : camera.Size;
                fromOrthographic = true;
                toOrthographic = hasSaved3DState ? saved3DOrthographic : false;
            }

            target2D = enable2D;
            active = true;
            startTime = EditorApplication.timeSinceStartup;
            repaintCallback = requestRepaint;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public void Apply(MontageViewportCamera camera)
        {
            if (!active || camera == null)
                return;

            float t = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - startTime) / Duration));
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            camera.Rotation = Quaternion.Slerp(fromRotation, toRotation, smooth);
            camera.Size = Mathf.Lerp(fromSize, toSize, smooth);
            camera.Orthographic = Mathf.Lerp(fromOrthographic ? 1f : 0f, toOrthographic ? 1f : 0f, smooth) >= 0.5f;

            if (t >= 1f)
                Complete(camera);
        }

        public void Shutdown()
        {
            EditorApplication.update -= Tick;
            active = false;
            repaintCallback = null;
        }

        private void Tick()
        {
            repaintCallback?.Invoke();
        }

        private void Complete(MontageViewportCamera camera)
        {
            camera.Rotation = toRotation;
            camera.Size = toSize;
            camera.Orthographic = toOrthographic;
            camera.Is2DMode = target2D;
            active = false;
            EditorApplication.update -= Tick;
        }
    }
}
