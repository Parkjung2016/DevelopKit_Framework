using System;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    /// <summary>
    /// Unity Scene View와 동일한 프리뷰 내비게이션 (RMB Fly + WASD, Orbit, Pan, Zoom).
    /// </summary>
    internal static class MontageViewportInput
    {
        private static readonly int ViewportControlHash = "MontageViewportControl".GetHashCode();

        private const float kFlySpeed = 9f;
        private const float kFlySpeedAcceleration = 1.8f;
        private const float kDefaultEasingDuration = 0.2f;
        private const float kScrollWheelMultiplier = 0.01f;
        private const float kSpeedMin = 0.01f;
        private const float kSpeedMax = 2f;

        private static bool flyLookActive;
        private static bool flyForward;
        private static bool flyBackward;
        private static bool flyLeft;
        private static bool flyRight;
        private static bool flyUp;
        private static bool flyDown;
        private static bool shiftHeld;
        private static float speedNormalized = 0.5f;
        private static bool accelerationEnabled = true;
        private static bool easingEnabled = true;
        private static float easingDuration = kDefaultEasingDuration;

        private static Vector3 smoothedFlyVelocity;
        private static Vector3 flyVelocitySmoothDamp;
        private static float flySpeedTarget;
        private static double lastMoveTime;
        private static bool updateRegistered;
        private static Action repaintCallback;
        private static MontageViewportCamera activeCamera;
        private static Func<bool> tryTogglePlayback;
        private static int lastPlaybackToggleFrame = -1;

        public static bool IsActive => flyLookActive || IsFlyMoving() || HasFlyMomentum();

        public static bool IsViewportEngaged =>
            flyLookActive || IsFlyMoving() || HasFlyMomentum() || GUIUtility.hotControl == ViewportControlHash;

        public static void SetPlaybackToggleHandler(Func<bool> handler) => tryTogglePlayback = handler;

        public static bool TryInvokePlaybackToggle() => InvokePlaybackToggle();

        public static bool Handle(Rect viewportRect, MontageViewportCamera camera, Action requestRepaint)
        {
            activeCamera = camera;
            repaintCallback = requestRepaint;

            if (MontageSceneViewNavigation.IsToolbarRect(viewportRect, Event.current.mousePosition)
                && Event.current.type is EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag)
            {
                return false;
            }

            int controlId = GUIUtility.GetControlID(ViewportControlHash, FocusType.Passive);
            Event evt = Event.current;
            bool inRect = viewportRect.Contains(evt.mousePosition);
            bool changed = false;

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (inRect && !EditorGUIUtility.editingTextField && ShouldCaptureMouseDown(evt, camera))
                    {
                        if (TryBeginFlyLook(evt, camera))
                            changed = true;

                        GUIUtility.hotControl = controlId;
                        GUIUtility.keyboardControl = controlId;
                        evt.Use();
                        changed = true;
                    }

                    break;

                case EventType.MouseUp:
                    if (TryEndFlyLook(evt))
                        changed = true;

                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        if (GUIUtility.keyboardControl == controlId)
                            GUIUtility.keyboardControl = 0;
                        evt.Use();
                        changed = true;
                    }

                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId || flyLookActive)
                    {
                        if (TryDrag(camera, evt))
                        {
                            evt.Use();
                            changed = true;
                        }
                    }

                    break;

                case EventType.ScrollWheel:
                    if (inRect)
                    {
                        if (flyLookActive)
                        {
                            ApplyFlySpeedScroll(evt);
                            evt.Use();
                            changed = true;
                        }
                        else if (TryZoom(camera, evt))
                        {
                            evt.Use();
                            changed = true;
                        }
                    }

                    break;

                case EventType.KeyDown:
                    if (TryHandlePlaybackShortcut(viewportRect, controlId, inRect))
                    {
                        evt.Use();
                        changed = true;
                        break;
                    }

                    if (evt.keyCode == KeyCode.LeftShift || evt.keyCode == KeyCode.RightShift)
                        shiftHeld = true;

                    if (flyLookActive && !EditorGUIUtility.editingTextField && TrySetFlyKey(evt.keyCode, true))
                    {
                        evt.Use();
                        changed = true;
                    }

                    break;

                case EventType.KeyUp:
                    if (evt.keyCode == KeyCode.LeftShift || evt.keyCode == KeyCode.RightShift)
                        shiftHeld = false;

                    if (TrySetFlyKey(evt.keyCode, false))
                    {
                        evt.Use();
                        changed = true;
                    }

                    break;

                case EventType.Layout:
                case EventType.Repaint:
                    if (flyLookActive && ApplyFlyMove(camera))
                        changed = true;

                    break;
            }

            UpdateEditorLoop();
            return changed;
        }

        public static void CancelInteraction()
        {
            EndFlyLook();
            if (GUIUtility.hotControl == ViewportControlHash)
            {
                GUIUtility.hotControl = 0;
                GUIUtility.keyboardControl = 0;
            }
        }

        public static void DrawOverlay(Rect viewportRect) =>
            MontageViewportFlyNotification.Draw(viewportRect);

        public static void Shutdown()
        {
            EndFlyLook();
            MontageSceneViewNavigation.Shutdown();
            MontageViewportFlyNotification.Clear();
            EditorApplication.update -= OnEditorUpdate;
            updateRegistered = false;
            repaintCallback = null;
            activeCamera = null;
            tryTogglePlayback = null;
            lastPlaybackToggleFrame = -1;
        }

        private static bool TryHandlePlaybackShortcut(Rect viewportRect, int controlId, bool inRect)
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || evt.keyCode != KeyCode.Space || EditorGUIUtility.editingTextField)
                return false;

            if (tryTogglePlayback == null)
                return false;

            bool viewportContext = flyLookActive
                || GUIUtility.hotControl == controlId
                || GUIUtility.keyboardControl == controlId
                || inRect;
            if (!viewportContext)
                return false;

            return InvokePlaybackToggle();
        }

        private static bool InvokePlaybackToggle()
        {
            if (tryTogglePlayback == null)
                return false;

            int frame = Time.frameCount;
            if (frame == lastPlaybackToggleFrame)
                return true;

            if (!tryTogglePlayback.Invoke())
                return false;

            lastPlaybackToggleFrame = frame;
            return true;
        }

        private static bool ShouldCaptureMouseDown(Event evt, MontageViewportCamera camera)
        {
            bool alt = evt.alt;
            bool action = (evt.modifiers & (EventModifiers.Command | EventModifiers.Control)) != 0;

            if (camera.Is2DMode || camera.IsRotationLocked)
                return evt.button is 0 or 1 or 2;

            return evt.button switch
            {
                0 => alt,
                1 => true,
                2 => true,
                _ => false
            } || (action && evt.button is 0 or 2);
        }

        private static bool TryBeginFlyLook(Event evt, MontageViewportCamera camera)
        {
            if (camera.Is2DMode || camera.IsRotationLocked)
                return false;

            if (evt.button != 1 || evt.alt)
                return false;

            flyLookActive = true;
            lastMoveTime = EditorApplication.timeSinceStartup;
            EditorGUIUtility.SetWantsMouseJumping(1);
            return true;
        }

        private static bool TryEndFlyLook(Event evt)
        {
            if (!flyLookActive || evt.button != 1)
                return false;

            EndFlyLook();
            return true;
        }

        private static void EndFlyLook()
        {
            if (!flyLookActive)
                return;

            flyLookActive = false;
            ClearFlyKeys();
            ResetFlyMotion();
            EditorGUIUtility.SetWantsMouseJumping(0);
        }

        private static void ResetFlyMotion()
        {
            smoothedFlyVelocity = Vector3.zero;
            flyVelocitySmoothDamp = Vector3.zero;
            flySpeedTarget = 0f;
        }

        private static bool TryDrag(MontageViewportCamera camera, Event evt)
        {
            if (flyLookActive && evt.button == 1 && !evt.alt)
                return ApplyFlyLookDrag(camera, evt);

            if (TryOrbit(camera, evt))
                return true;

            if (TryPan(camera, evt))
                return true;

            return TryZoomDrag(camera, evt);
        }

        private static bool ApplyFlyLookDrag(MontageViewportCamera camera, Event evt)
        {
            if (camera.Is2DMode || camera.IsRotationLocked)
                return false;

            Quaternion rotation = camera.Rotation;
            rotation = Quaternion.AngleAxis(evt.delta.y * 0.003f * Mathf.Rad2Deg, rotation * Vector3.right) * rotation;
            rotation = Quaternion.AngleAxis(evt.delta.x * 0.003f * Mathf.Rad2Deg, Vector3.up) * rotation;
            camera.Rotation = rotation;
            return true;
        }

        private static bool TryOrbit(MontageViewportCamera camera, Event evt)
        {
            if (camera.Is2DMode || camera.IsRotationLocked)
                return false;

            if (!evt.alt || evt.button != 0)
                return false;

            Quaternion yaw = Quaternion.AngleAxis(evt.delta.x * 0.5f, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(evt.delta.y * 0.5f, camera.Rotation * Vector3.right);
            camera.Rotation = pitch * yaw * camera.Rotation;
            return true;
        }

        private static bool TryPan(MontageViewportCamera camera, Event evt)
        {
            bool panWithMiddle = evt.button == 2;
            bool panWithAltRight = evt.alt && evt.button == 1;
            bool panWithRightIn2D = (camera.Is2DMode || camera.IsRotationLocked) && evt.button == 1 && !evt.alt;
            bool panWithAltLeftIn2D = camera.Is2DMode && evt.button == 0 && evt.alt;

            if (!panWithMiddle && !panWithAltRight && !panWithRightIn2D && !panWithAltLeftIn2D)
                return false;

            float scale = Mathf.Max(0.001f, camera.Size * 0.003f);
            Vector3 right = camera.Is2DMode ? Vector3.right : camera.Rotation * Vector3.right;
            Vector3 up = camera.Is2DMode ? Vector3.up : camera.Rotation * Vector3.up;
            camera.Pivot -= (right * evt.delta.x + up * -evt.delta.y) * scale;
            return true;
        }

        private static bool TryZoomDrag(MontageViewportCamera camera, Event evt)
        {
            if (!evt.alt || evt.button != 1 || camera.Is2DMode)
                return false;

            float zoomDelta = HandleUtility.niceMouseDeltaZoom * (evt.shift ? 9f : 3f);
            camera.Size = Mathf.Clamp(camera.Size + zoomDelta * Mathf.Max(Mathf.Abs(camera.Size), 0.3f) * 0.003f, 0.05f, 1000f);
            return true;
        }

        private static bool TryZoom(MontageViewportCamera camera, Event evt)
        {
            float zoomFactor = 1f + evt.delta.y * 0.03f;
            camera.Size = Mathf.Clamp(camera.Size * zoomFactor, 0.05f, 1000f);
            return true;
        }

        private static void ApplyFlySpeedScroll(Event evt)
        {
            float scrollDelta = evt.delta.y;
            if ((evt.modifiers & EventModifiers.Shift) != 0 &&
                (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor))
            {
                scrollDelta = evt.delta.x;
            }

            speedNormalized = Mathf.Clamp01(speedNormalized - scrollDelta * kScrollWheelMultiplier);
            MontageViewportFlyNotification.ShowSpeed(GetCameraSpeed(), accelerationEnabled);
        }

        private static bool TrySetFlyKey(KeyCode keyCode, bool pressed)
        {
            switch (keyCode)
            {
                case KeyCode.W: flyForward = pressed; return flyLookActive;
                case KeyCode.S: flyBackward = pressed; return flyLookActive;
                case KeyCode.A: flyLeft = pressed; return flyLookActive;
                case KeyCode.D: flyRight = pressed; return flyLookActive;
                case KeyCode.E: flyUp = pressed; return flyLookActive;
                case KeyCode.Q: flyDown = pressed; return flyLookActive;
                default: return false;
            }
        }

        private static void ClearFlyKeys()
        {
            flyForward = false;
            flyBackward = false;
            flyLeft = false;
            flyRight = false;
            flyUp = false;
            flyDown = false;
        }

        private static bool IsFlyMoving() =>
            flyForward || flyBackward || flyLeft || flyRight || flyUp || flyDown;

        private static bool HasFlyMomentum() =>
            smoothedFlyVelocity.sqrMagnitude > 1e-8f || flySpeedTarget > 1e-6f;

        private static Vector3 BuildLocalInput()
        {
            Vector3 local = Vector3.zero;
            if (flyForward) local.z += 1f;
            if (flyBackward) local.z -= 1f;
            if (flyLeft) local.x -= 1f;
            if (flyRight) local.x += 1f;
            if (flyUp) local.y += 1f;
            if (flyDown) local.y -= 1f;
            return local;
        }

        private static float GetCameraSpeed() =>
            Mathf.Lerp(kSpeedMin, kSpeedMax, speedNormalized);

        private static bool ApplyFlyMove(MontageViewportCamera camera)
        {
            if (!flyLookActive)
                return false;

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = lastMoveTime > 0d ? (float)(now - lastMoveTime) : 0.016f;
            lastMoveTime = now;
            deltaTime = Mathf.Clamp(deltaTime, 0.001f, 0.033f);

            Vector3 localInput = BuildLocalInput();
            bool moving = localInput.sqrMagnitude > Mathf.Epsilon;

            float speedModifier = GetCameraSpeed();
            if (shiftHeld)
                speedModifier *= 5f;

            if (moving)
            {
                if (accelerationEnabled)
                {
                    flySpeedTarget = flySpeedTarget < Mathf.Epsilon
                        ? kFlySpeed
                        : flySpeedTarget * Mathf.Pow(kFlySpeedAcceleration, deltaTime);
                }
                else
                {
                    flySpeedTarget = kFlySpeed;
                }
            }
            else
            {
                flySpeedTarget = 0f;
            }

            Vector3 targetVelocity = moving
                ? localInput.normalized * flySpeedTarget * speedModifier
                : Vector3.zero;

            if (easingEnabled)
            {
                smoothedFlyVelocity = Vector3.SmoothDamp(
                    smoothedFlyVelocity,
                    targetVelocity,
                    ref flyVelocitySmoothDamp,
                    easingDuration,
                    Mathf.Infinity,
                    deltaTime);
            }
            else
            {
                smoothedFlyVelocity = targetVelocity;
            }

            if (smoothedFlyVelocity.sqrMagnitude <= 1e-10f)
                return moving;

            camera.Pivot += camera.Rotation * smoothedFlyVelocity * deltaTime;
            return true;
        }

        private static void UpdateEditorLoop()
        {
            bool needsUpdate = flyLookActive
                || HasFlyMomentum()
                || MontageViewportFlyNotification.IsVisible
                || GUIUtility.hotControl == ViewportControlHash;
            if (needsUpdate && !updateRegistered)
            {
                EditorApplication.update += OnEditorUpdate;
                updateRegistered = true;
            }
            else if (!needsUpdate && updateRegistered)
            {
                EditorApplication.update -= OnEditorUpdate;
                updateRegistered = false;
            }
        }

        private static void OnEditorUpdate()
        {
            if (IsViewportEngaged
                && !EditorGUIUtility.editingTextField
                && MontageEditorKeyboardInput.WasSpacePressedThisFrame())
            {
                InvokePlaybackToggle();
            }

            bool moved = false;
            if (flyLookActive && activeCamera != null)
                moved = ApplyFlyMove(activeCamera);

            if (flyLookActive || GUIUtility.hotControl == ViewportControlHash || moved || MontageViewportFlyNotification.IsVisible)
                repaintCallback?.Invoke();

            if (!flyLookActive && !HasFlyMomentum() && !MontageViewportFlyNotification.IsVisible
                && GUIUtility.hotControl != ViewportControlHash)
                UpdateEditorLoop();
        }
    }
}
