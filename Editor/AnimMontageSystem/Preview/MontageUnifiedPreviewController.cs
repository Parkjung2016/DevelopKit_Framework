using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageUnifiedPreviewController : System.IDisposable
    {
        private PreviewRenderUtility preview;
        private GameObject previewInstance;
        private GameObject gridInstance;
        private GameObject gizmoInstance;
        private Texture previewTexture;
        private MontageEditorContext boundContext;
        private readonly MontageViewportCamera viewportCamera = new();
        private readonly MontageSceneViewBridge sceneViewBridge = new();
        private Bounds renderBounds;
        private bool hasBounds;
        private Transform previewMotionRoot;
        private float lockedPreviewRootY;
        private float lockedMotionRootLocalY;
        private float previewGroundPlaneY;
        private bool hasPreviewHeightLock;

        public void Bind(MontageEditorContext editorContext) => boundContext = editorContext;

        public void SetPreviewModel(GameObject prefab)
        {
            ClearInstance();
            previewTexture = null;

            if (prefab == null)
                return;

            EnsurePreview();
            previewInstance = preview.InstantiatePrefabInScene(prefab);
            previewInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.AddSingleGO(previewInstance);
            MontagePreviewSampling.BindInstance(previewInstance);
            previewMotionRoot = previewInstance.GetComponentInChildren<Animator>()?.transform ?? previewInstance.transform;

            CacheBounds();
            AlignModelFeetToGround();
            CacheBounds();
            StorePreviewHeightLock();
            viewportCamera.FrameBounds(renderBounds);
            viewportCamera.Is2DMode = false;
        }

        private float GetFootPlaneY()
        {
            if (!hasBounds || previewInstance == null)
                return 0f;

            float minY = float.MaxValue;
            bool foundSkinned = false;

            foreach (SkinnedMeshRenderer skinned in previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinned == null || !skinned.enabled)
                    continue;

                minY = Mathf.Min(minY, skinned.bounds.min.y);
                foundSkinned = true;
            }

            if (foundSkinned)
                return minY;

            foreach (Renderer renderer in previewInstance.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                minY = Mathf.Min(minY, renderer.bounds.min.y);
            }

            return minY == float.MaxValue ? 0f : minY;
        }

        private void SnapModelFeetToGrid()
        {
            if (previewInstance == null)
                return;

            float footY = GetFootPlaneY();
            float deltaY = -footY;
            if (Mathf.Abs(deltaY) <= 0.00001f)
                return;

            previewInstance.transform.position += new Vector3(0f, deltaY, 0f);
        }

        public void Sample(MontageEditorContext editorContext) =>
            MontagePreviewSampling.TrySample(previewInstance, editorContext ?? boundContext);

        public void DrawPreview(Rect rect, System.Action requestRepaint)
        {
            if (!ShaderUtil.hardwareSupportsRectRenderTexture)
            {
                EditorGUI.HelpBox(rect, "Preview is not supported on this GPU.", MessageType.Warning);
                return;
            }

            if (previewInstance == null)
            {
                DrawEmptyState(rect, "Assign a Preview Mesh prefab.");
                MontageSceneViewNavigation.DrawToolbar(rect, viewportCamera, requestRepaint);
                return;
            }

            GUI.BeginGroup(rect);
            Rect localRect = new(0f, 0f, rect.width, rect.height);

            MontageSceneViewNavigation.ApplyModeTransition(viewportCamera);

            bool inputChanged = MontageViewportInput.Handle(localRect, viewportCamera, requestRepaint);

            if (TryFrameModelOnKey(requestRepaint))
                inputChanged = true;

            if (!viewportCamera.Is2DMode && !MontageViewportInput.IsActive && !MontageSceneViewNavigation.IsModeTransitionActive)
            {
                sceneViewBridge.Prepare(viewportCamera, localRect, localRect.Contains(Event.current.mousePosition));
                if (sceneViewBridge.HandleOrientationGizmo(viewportCamera))
                    inputChanged = true;
            }

            if (inputChanged)
                requestRepaint?.Invoke();

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.clickCount == 2 && evt.button == 0 && hasBounds
                && localRect.Contains(evt.mousePosition))
            {
                Sample(boundContext);
                StabilizePreviewHeight();
                CacheBounds();
                viewportCamera.FrameBounds(renderBounds);
                requestRepaint?.Invoke();
            }

            if (evt.type == EventType.Repaint)
            {
                Sample(boundContext);
                StabilizePreviewHeight();
                CacheBounds();

                if (previewInstance != null)
                    MontageEditorSelectionUtility.RemoveHierarchyFromSelection(previewInstance);

                bool modeTransitionActive = MontageSceneViewNavigation.IsModeTransitionActive;

                preview.BeginPreview(localRect, GUIStyle.none);
                viewportCamera.ApplyToPreviewCamera(preview.camera, modeTransitionActive);
                preview.camera.cameraType = CameraType.SceneView;
                preview.camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.ambientColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                float groundPlaneY = hasPreviewHeightLock ? previewGroundPlaneY : 0f;
                MontagePreviewSceneGizmos.Update(
                    gizmoInstance,
                    previewInstance,
                    renderBounds,
                    hasBounds,
                    groundPlaneY,
                    MontageSceneViewNavigation.ShouldUseFrontGrid(viewportCamera));
                MontageSceneViewGrid.DrawPreview(
                    preview.camera,
                    gridInstance,
                    viewportCamera,
                    MontageSceneViewNavigation.ShouldUseFrontGrid(viewportCamera),
                    groundPlaneY);
                previewTexture = preview.EndPreview();
            }

            if (previewTexture != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(localRect, previewTexture, ScaleMode.StretchToFill, false);

            if (Event.current.type == EventType.Repaint
                && !viewportCamera.Is2DMode
                && !MontageViewportInput.IsActive
                && !MontageSceneViewNavigation.IsModeTransitionActive
                && sceneViewBridge.IsAvailable)
            {
                sceneViewBridge.Prepare(viewportCamera, localRect, localRect.Contains(Event.current.mousePosition));
                sceneViewBridge.HandleOrientationGizmo(viewportCamera);
            }

            if (MontageSceneViewNavigation.DrawToolbar(localRect, viewportCamera, requestRepaint))
                requestRepaint?.Invoke();

            MontageViewportInput.DrawOverlay(localRect);

            if (!viewportCamera.Is2DMode && hasBounds)
            {
                Rect hintRect = new(6f, localRect.yMax - 20f, localRect.width - 12f, 16f);
                EditorGUI.DropShadowLabel(
                    hintRect,
                    "Space Play | F Frame | RMB Look + WASD | Alt+LMB Orbit | MMB Pan | Scroll Zoom");
            }

            GUI.EndGroup();
        }

        private bool TryFrameModelOnKey(System.Action requestRepaint)
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || evt.keyCode != KeyCode.F || EditorGUIUtility.editingTextField)
                return false;

            if (!hasBounds)
                return false;

            Sample(boundContext);
            StabilizePreviewHeight();
            CacheBounds();
            viewportCamera.FrameBounds(renderBounds);
            evt.Use();
            requestRepaint?.Invoke();
            return true;
        }

        public void Dispose()
        {
            MontageViewportInput.Shutdown();
            MontageSceneViewNavigation.Shutdown();
            sceneViewBridge.Dispose();
            MontagePreviewSampling.Dispose();
            ClearInstance();
            ClearGrid();
            ClearGizmos();

            if (previewTexture != null)
            {
                Object.DestroyImmediate(previewTexture);
                previewTexture = null;
            }

            if (preview == null)
                return;

            preview.Cleanup();
            preview = null;
        }

        private void EnsurePreview()
        {
            if (preview != null)
            {
                EnsureGrid();
                EnsureGizmos();
                return;
            }

            preview = new PreviewRenderUtility(true);
            preview.cameraFieldOfView = 30f;
            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            preview.lights[1].intensity = 0.55f;

            EnsureGrid();
            EnsureGizmos();
        }

        private void EnsureGrid()
        {
            if (preview == null || gridInstance != null)
                return;

            gridInstance = MontageSceneViewGrid.CreatePreviewGrid();
            if (gridInstance != null)
                preview.AddSingleGO(gridInstance);
        }

        private void EnsureGizmos()
        {
            if (preview == null || gizmoInstance != null)
                return;

            gizmoInstance = MontagePreviewSceneGizmos.Create();
            if (gizmoInstance != null)
                preview.AddSingleGO(gizmoInstance);
        }

        private void CacheBounds()
        {
            hasBounds = false;
            if (previewInstance == null)
                return;

            renderBounds = new Bounds(previewInstance.transform.position, Vector3.zero);
            foreach (Renderer renderer in previewInstance.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    renderBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    renderBounds.Encapsulate(renderer.bounds);
                }
            }

        }

        private void AlignModelFeetToGround() => SnapModelFeetToGrid();

        private void StorePreviewHeightLock()
        {
            if (previewInstance == null)
            {
                hasPreviewHeightLock = false;
                return;
            }

            previewGroundPlaneY = 0f;
            lockedPreviewRootY = previewInstance.transform.position.y;
            lockedMotionRootLocalY = previewMotionRoot != null ? previewMotionRoot.localPosition.y : 0f;
            hasPreviewHeightLock = true;
        }

        private void StabilizePreviewHeight()
        {
            if (!hasPreviewHeightLock || previewInstance == null)
                return;

            Vector3 rootPosition = previewInstance.transform.position;
            rootPosition.y = lockedPreviewRootY;
            previewInstance.transform.position = rootPosition;

            if (previewMotionRoot == null || previewMotionRoot == previewInstance.transform)
                return;

            Vector3 localPosition = previewMotionRoot.localPosition;
            localPosition.y = lockedMotionRootLocalY;
            previewMotionRoot.localPosition = localPosition;
        }

        private static void DrawEmptyState(Rect rect, string message)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            GUI.Label(rect, message, EditorStyles.centeredGreyMiniLabel);
        }

        private void ClearInstance()
        {
            if (previewInstance == null)
                return;

            MontageEditorSelectionUtility.RemoveHierarchyFromSelection(previewInstance);
            Object.DestroyImmediate(previewInstance);
            previewInstance = null;
            previewMotionRoot = null;
            hasBounds = false;
            hasPreviewHeightLock = false;
        }

        private void ClearGrid()
        {
            if (gridInstance == null)
                return;

            if (gridInstance.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                Object.DestroyImmediate(filter.sharedMesh);

            Object.DestroyImmediate(gridInstance);
            gridInstance = null;
        }

        private void ClearGizmos()
        {
            if (gizmoInstance == null)
                return;

            MontagePreviewSceneGizmos.Destroy(gizmoInstance);
            gizmoInstance = null;
        }
    }
}
