using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageUnifiedPreviewController : System.IDisposable
    {
        private PreviewRenderUtility preview;
        private GameObject previewInstance;
        private GameObject gridInstance;
        private GameObject gizmoInstance;
        private GameObject shadowReceiverInstance;
        private Material shadowReceiverMaterial;
        private GameObject projectedShadowInstance;
        private Mesh projectedShadowMesh;
        private Mesh bakedShadowMesh;
        private Material projectedShadowMaterial;
        private Material previewSkyboxMaterial;
        private readonly List<Vector3> projectedShadowVertices = new();
        private readonly List<int> projectedShadowIndices = new();
        private readonly List<Color> projectedShadowColors = new();
        private Texture previewTexture;
        private MontageEditorContext boundContext;
        private readonly MontageViewportCamera viewportCamera = new();
        private readonly MontageSceneViewBridge sceneViewBridge = new();
        private readonly MontageAnimatorRootMotionPreviewSampler rootMotionSampler = new();
        private Bounds renderBounds;
        private bool hasBounds;
        private Transform previewMotionRoot;
        private Vector3 initialPreviewPosition;
        private Quaternion initialPreviewRotation;
        private Vector3 initialPreviewScale;
        private float previewAnimationSampleTime;
        private float lastPreviewPlayheadTime;

        private MontageTimelineElementEvaluation previousPreviewTimelineElementEvaluation =
            MontageTimelineElementEvaluation.Default;

        private Vector3 initialMotionRootLocalPosition;
        private Quaternion initialMotionRootLocalRotation;
        private Vector3 initialMotionRootLocalScale;
        private bool hasInitialPreviewTransform;
        private float lockedPreviewRootY;
        private float lockedMotionRootLocalY;
        private float previewGroundPlaneY;
        private bool hasPreviewHeightLock;
        private double lastEffectCacheTime;
        private readonly List<ParticlePreviewEffect> previewParticleSystems = new();
        private readonly List<VisualEffectPreviewEffect> previewVisualEffects = new();
        private readonly List<ParticleSystem> particleCacheBuffer = new();
        private readonly List<VisualEffect> visualEffectCacheBuffer = new();

        public GameObject NotifyOwner => previewInstance;

        public Animator NotifyAnimator => previewInstance != null
            ? previewInstance.GetComponentInChildren<Animator>()
            : null;

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
            ConfigureShadowCasting(previewInstance);
            preview.AddSingleGO(previewInstance);
            RefreshPreviewEffectCache(true);
            MontagePreviewSampling.BindInstance(previewInstance);
            previewMotionRoot = previewInstance.GetComponentInChildren<Animator>()?.transform ??
                                previewInstance.transform;

            CacheBounds();
            AlignModelFeetToGround();
            StoreInitialPreviewTransform();
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
            SamplePreviewPose(editorContext ?? boundContext);

        private void SamplePreviewPose(MontageEditorContext context)
        {
            RevertTimelineElementPreviewTransform();
            float sampleTime = GetPreviewAnimationSampleTime(context);
            MontagePreviewSampling.TrySample(previewInstance, context, sampleTime);
            ApplyRootMotionPreviewTransform(context, sampleTime);
            StabilizePreviewTransform();
            ApplyTimelineElementPreviewTransform(context);
        }

        private float GetPreviewAnimationSampleTime(MontageEditorContext context)
        {
            if (context?.Montage == null)
            {
                previewAnimationSampleTime = 0f;
                lastPreviewPlayheadTime = 0f;
                return 0f;
            }

            if (!context.IsPlaying)
            {
                previewAnimationSampleTime = context.PlayheadTime;
                lastPreviewPlayheadTime = context.PlayheadTime;
                return previewAnimationSampleTime;
            }

            float playheadDelta = context.PlayheadTime - lastPreviewPlayheadTime;
            if (playheadDelta < 0f)
            {
                previewAnimationSampleTime = context.PlayheadTime;
                lastPreviewPlayheadTime = context.PlayheadTime;
                return previewAnimationSampleTime;
            }

            MontageTimelineElementEvaluation evaluation =
                MontageTimelineElementEvaluator.Evaluate(context.Montage, lastPreviewPlayheadTime);
            previewAnimationSampleTime = Mathf.Clamp(
                previewAnimationSampleTime + playheadDelta * evaluation.TimeScaleMultiplier,
                0f,
                context.Montage.Length);
            lastPreviewPlayheadTime = context.PlayheadTime;
            return previewAnimationSampleTime;
        }

        public void ResetRootMotionPreviewPose()
        {
            if (previewInstance == null || !hasInitialPreviewTransform)
                return;

            MontagePreviewSampling.Reset();
            previewAnimationSampleTime = 0f;
            previousPreviewTimelineElementEvaluation = MontageTimelineElementEvaluation.Default;
            previewInstance.transform.SetPositionAndRotation(initialPreviewPosition, initialPreviewRotation);
            previewInstance.transform.localScale = initialPreviewScale;

            if (previewMotionRoot != null && previewMotionRoot != previewInstance.transform)
            {
                previewMotionRoot.localPosition = initialMotionRootLocalPosition;
                previewMotionRoot.localRotation = initialMotionRootLocalRotation;
                previewMotionRoot.localScale = initialMotionRootLocalScale;
            }

            StorePreviewHeightLock();
            CacheBounds();
        }

        public void DrawPreview(Rect rect, System.Action requestRepaint)
        {
            if (!ShaderUtil.hardwareSupportsRectRenderTexture)
            {
                EditorGUI.HelpBox(rect, "Preview is not supported on this GPU.", MessageType.Warning);
                return;
            }

            if (IsPlayModePreviewBlocked())
            {
                DrawEmptyState(rect, "Play Mode 중에는 Montage Preview를 사용할 수 없습니다.");
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

            if (!viewportCamera.Is2DMode && !MontageViewportInput.IsActive &&
                !MontageSceneViewNavigation.IsModeTransitionActive)
            {
                sceneViewBridge.Prepare(viewportCamera, localRect, localRect.Contains(Event.current.mousePosition));
                if (sceneViewBridge.HandleOrientationGizmo(viewportCamera))
                    inputChanged = true;
            }

            if (inputChanged)
                requestRepaint?.Invoke();
            Event evt = Event.current;

            if (evt.type == EventType.Repaint)
            {
                Sample(boundContext);
                SimulatePreviewEffects();
                CacheBounds();

                if (previewInstance != null)
                    MontageEditorSelectionUtility.RemoveHierarchyFromSelection(previewInstance);

                bool modeTransitionActive = MontageSceneViewNavigation.IsModeTransitionActive;

                preview.BeginPreview(localRect, GUIStyle.none);
                viewportCamera.ApplyToPreviewCamera(preview.camera, modeTransitionActive);
                preview.camera.cameraType = CameraType.SceneView;
                preview.camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
                ApplyPreviewSkybox();
                ApplyPreviewLighting();
                float groundPlaneY = hasPreviewHeightLock ? previewGroundPlaneY : 0f;
                UpdateShadowReceiver(groundPlaneY);
                HideProjectedMeshShadow();
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
                    groundPlaneY,
                    MontageSceneViewNavigation.GridHalfSize,
                    MontageSceneViewNavigation.GridStep);
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
            if (evt.type != EventType.KeyDown || evt.keyCode != KeyCode.F)
                return false;

            if (!MontageViewportInput.IsViewportEngaged && EditorGUIUtility.editingTextField)
                return false;

            if (!hasBounds)
                return false;

            Sample(boundContext);
            CacheBounds();
            viewportCamera.FrameBounds(renderBounds);
            evt.Use();
            requestRepaint?.Invoke();
            return true;
        }

        private void SimulatePreviewEffects()
        {
            if (previewInstance == null)
                return;

            RefreshPreviewEffectCache(false);
            double now = EditorApplication.timeSinceStartup;
            SimulateParticleSystems(now);
            SimulateVisualEffects(now);
        }

        private void RefreshPreviewEffectCache(bool force)
        {
            if (previewInstance == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (!force && now - lastEffectCacheTime < 0.05)
                return;

            lastEffectCacheTime = now;
            particleCacheBuffer.Clear();
            visualEffectCacheBuffer.Clear();
            previewInstance.GetComponentsInChildren(true, particleCacheBuffer);
            previewInstance.GetComponentsInChildren(true, visualEffectCacheBuffer);

            for (int i = previewParticleSystems.Count - 1; i >= 0; i--)
            {
                if (previewParticleSystems[i].ParticleSystem == null
                    || !particleCacheBuffer.Contains(previewParticleSystems[i].ParticleSystem))
                {
                    previewParticleSystems.RemoveAt(i);
                }
            }

            for (int i = previewVisualEffects.Count - 1; i >= 0; i--)
            {
                if (previewVisualEffects[i].VisualEffect == null
                    || !visualEffectCacheBuffer.Contains(previewVisualEffects[i].VisualEffect))
                {
                    previewVisualEffects.RemoveAt(i);
                }
            }

            for (int i = 0; i < particleCacheBuffer.Count; i++)
            {
                ParticleSystem particleSystem = particleCacheBuffer[i];
                if (particleSystem != null && !ContainsParticleSystem(particleSystem))
                    previewParticleSystems.Add(new ParticlePreviewEffect(particleSystem, now));
            }

            for (int i = 0; i < visualEffectCacheBuffer.Count; i++)
            {
                VisualEffect visualEffect = visualEffectCacheBuffer[i];
                if (visualEffect != null && !ContainsVisualEffect(visualEffect))
                    previewVisualEffects.Add(new VisualEffectPreviewEffect(visualEffect, now));
            }
        }

        private bool ContainsParticleSystem(ParticleSystem particleSystem)
        {
            for (int i = 0; i < previewParticleSystems.Count; i++)
            {
                if (previewParticleSystems[i].ParticleSystem == particleSystem)
                    return true;
            }

            return false;
        }

        private bool ContainsVisualEffect(VisualEffect visualEffect)
        {
            for (int i = 0; i < previewVisualEffects.Count; i++)
            {
                if (previewVisualEffects[i].VisualEffect == visualEffect)
                    return true;
            }

            return false;
        }

        private void SimulateParticleSystems(double now)
        {
            for (int i = previewParticleSystems.Count - 1; i >= 0; i--)
            {
                ParticlePreviewEffect effect = previewParticleSystems[i];
                if (effect.ParticleSystem == null)
                {
                    previewParticleSystems.RemoveAt(i);
                    continue;
                }

                float deltaTime = Mathf.Clamp((float)(now - effect.LastUpdateTime), 0f, 0.05f);
                effect.LastUpdateTime = now;
                if (deltaTime > 0f)
                    effect.ParticleSystem.Simulate(deltaTime, true, false, false);
            }
        }

        private void SimulateVisualEffects(double now)
        {
            for (int i = previewVisualEffects.Count - 1; i >= 0; i--)
            {
                VisualEffectPreviewEffect effect = previewVisualEffects[i];
                if (effect.VisualEffect == null)
                {
                    previewVisualEffects.RemoveAt(i);
                    continue;
                }

                float deltaTime = Mathf.Clamp((float)(now - effect.LastUpdateTime), 0f, 0.05f);
                effect.LastUpdateTime = now;
                if (deltaTime > 0f)
                    effect.VisualEffect.Simulate(deltaTime);
            }
        }

        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingEditMode
                or PlayModeStateChange.EnteredPlayMode
                or PlayModeStateChange.ExitingPlayMode)
            {
                ResetPreviewRuntimeState(clearInstance: true);
                return;
            }

            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            ResetPreviewRuntimeState(clearInstance: true);

            if (boundContext?.PreviewModel == null)
                return;

            SetPreviewModel(boundContext.PreviewModel);
            ResetRootMotionPreviewPose();
            RefreshPreviewEffectCache(true);
            CacheBounds();
        }
        private static bool IsPlayModePreviewBlocked() =>
            EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode;

        private void ResetPreviewRuntimeState(bool clearInstance)
        {
            rootMotionSampler.Dispose();
            MontagePreviewSampling.Dispose();
            previousPreviewTimelineElementEvaluation = MontageTimelineElementEvaluation.Default;
            previewAnimationSampleTime = 0f;
            previewParticleSystems.Clear();
            previewVisualEffects.Clear();
            particleCacheBuffer.Clear();
            visualEffectCacheBuffer.Clear();
            hasBounds = false;
            hasPreviewHeightLock = false;
            hasInitialPreviewTransform = false;

            if (previewTexture != null)
            {
                Object.DestroyImmediate(previewTexture);
                previewTexture = null;
            }

            if (clearInstance)
                ClearInstance();
        }

        public void Dispose()
        {
            MontageViewportInput.Shutdown();
            MontageSceneViewNavigation.Shutdown();
            sceneViewBridge.Dispose();
            rootMotionSampler.Dispose();
            MontagePreviewSampling.Dispose();
            ClearInstance();
            ClearGrid();
            ClearGizmos();
            ClearShadowReceiver();
            ClearProjectedMeshShadow();
            ClearPreviewSkybox();

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
                EnsureShadowReceiver();
                EnsureProjectedMeshShadow();
                return;
            }

            preview = new PreviewRenderUtility(true);
            preview.cameraFieldOfView = 30f;
            ApplyPreviewLighting();

            EnsureGrid();
            EnsureGizmos();
            EnsureShadowReceiver();
            EnsureProjectedMeshShadow();
        }


        private void ApplyPreviewSkybox()
        {
            if (preview?.camera == null)
                return;

            Skybox skybox = preview.camera.GetComponent<Skybox>();
            if (MontageSceneViewNavigation.SkyboxEnabled)
            {
                Material sceneSkybox = MontageSceneViewNavigation.SkyboxMaterial;
                if (sceneSkybox == null)
                    sceneSkybox = GetPreviewSkyboxMaterial();

                if (sceneSkybox != null)
                {
                    if (skybox == null)
                        skybox = preview.camera.gameObject.AddComponent<Skybox>();

                    skybox.material = sceneSkybox;
                    skybox.enabled = true;
                    preview.camera.clearFlags = CameraClearFlags.Skybox;
                    return;
                }
            }

            if (skybox != null)
                skybox.enabled = false;

            preview.camera.clearFlags = CameraClearFlags.SolidColor;
        }


        private static Material GetSceneViewSkyboxMaterial()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Skybox sceneSkybox = sceneView.camera.GetComponent<Skybox>();
                if (sceneSkybox != null && sceneSkybox.enabled && sceneSkybox.material != null)
                    return sceneSkybox.material;
            }

            return RenderSettings.skybox;
        }

        private Material GetPreviewSkyboxMaterial()
        {
            if (previewSkyboxMaterial == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                    return null;

                previewSkyboxMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            ApplyPreviewSkyboxSettings(previewSkyboxMaterial);
            return previewSkyboxMaterial;
        }

        private static void ApplyPreviewSkyboxSettings(Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty("_SkyTint"))
                material.SetColor("_SkyTint", MontageSceneViewNavigation.SkyTint);
            if (material.HasProperty("_GroundColor"))
                material.SetColor("_GroundColor", MontageSceneViewNavigation.SkyGroundColor);
            if (material.HasProperty("_AtmosphereThickness"))
                material.SetFloat("_AtmosphereThickness", MontageSceneViewNavigation.SkyAtmosphere);
            if (material.HasProperty("_Exposure"))
                material.SetFloat("_Exposure", MontageSceneViewNavigation.SkyExposure);
        }

        private void ApplyPreviewLighting()
        {
            if (preview == null)
                return;

            bool enabled = MontageSceneViewNavigation.LightEnabled;
            float ambient = MontageSceneViewNavigation.LightAmbient;
            preview.ambientColor = new Color(ambient, ambient, ambient, 1f);

            if (preview.lights == null || preview.lights.Length == 0)
                return;

            Light keyLight = preview.lights[0];
            if (keyLight != null)
            {
                keyLight.enabled = enabled;
                keyLight.intensity = enabled ? MontageSceneViewNavigation.LightIntensity : 0f;
                keyLight.transform.rotation = MontageSceneViewNavigation.LightRotation;
                keyLight.shadows = enabled && MontageSceneViewNavigation.LightShadows
                    ? LightShadows.Soft
                    : LightShadows.None;
                keyLight.shadowStrength = 0.82f;
                keyLight.shadowBias = 0.02f;
                keyLight.shadowNormalBias = 0.08f;
                keyLight.shadowNearPlane = 0.05f;
            }

            if (preview.lights.Length <= 1)
                return;

            Light fillLight = preview.lights[1];
            if (fillLight == null)
                return;

            fillLight.enabled = enabled;
            fillLight.intensity = enabled ? MontageSceneViewNavigation.LightIntensity * 0.25f : 0f;
            fillLight.transform.rotation = Quaternion.Euler(-MontageSceneViewNavigation.LightPitch * 0.5f,
                MontageSceneViewNavigation.LightYaw + 160f, 0f);
            fillLight.shadows = LightShadows.None;
        }

        private void EnsureProjectedMeshShadow()
        {
            if (preview == null || projectedShadowInstance != null)
                return;

            projectedShadowInstance = new GameObject("Montage Preview Projected Mesh Shadow")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            projectedShadowMesh = new Mesh
            {
                name = "Montage Preview Projected Shadow Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            bakedShadowMesh = new Mesh
            {
                name = "Montage Preview Baked Shadow Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            projectedShadowInstance.AddComponent<MeshFilter>().sharedMesh = projectedShadowMesh;
            MeshRenderer renderer = projectedShadowInstance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetProjectedShadowMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            projectedShadowInstance.SetActive(false);
            preview.AddSingleGO(projectedShadowInstance);
        }

        private Material GetProjectedShadowMaterial()
        {
            if (projectedShadowMaterial != null)
                return projectedShadowMaterial;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            projectedShadowMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            projectedShadowMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            projectedShadowMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            projectedShadowMaterial.SetInt("_Cull", (int)CullMode.Off);
            projectedShadowMaterial.SetInt("_ZWrite", 0);
            projectedShadowMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            return projectedShadowMaterial;
        }


        private void HideProjectedMeshShadow()
        {
            if (projectedShadowInstance != null)
                projectedShadowInstance.SetActive(false);
        }

        private void UpdateProjectedMeshShadow(float groundPlaneY)
        {
            HideProjectedMeshShadow();
        }

        private void AddProjectedShadowMesh(Mesh source, Matrix4x4 localToWorld, Vector3 lightDirection, float shadowY)
        {
            if (source == null)
                return;

            Vector3[] vertices = source.vertices;
            int[] triangles = source.triangles;
            if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length == 0)
                return;

            int vertexOffset = projectedShadowVertices.Count;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = localToWorld.MultiplyPoint3x4(vertices[i]);
                float t = (shadowY - world.y) / lightDirection.y;
                Vector3 projected = world + lightDirection * t;
                projected.y = shadowY;
                projectedShadowVertices.Add(projected);
                projectedShadowColors.Add(new Color(0f, 0f, 0f, 0.34f));
            }

            for (int i = 0; i < triangles.Length; i++)
                projectedShadowIndices.Add(vertexOffset + triangles[i]);
        }

        private void EnsureShadowReceiver()
        {
            if (preview == null || shadowReceiverInstance != null)
                return;

            shadowReceiverInstance = GameObject.CreatePrimitive(PrimitiveType.Plane);
            shadowReceiverInstance.name = "Montage Preview Shadow Receiver";
            shadowReceiverInstance.hideFlags = HideFlags.HideAndDontSave;

            if (shadowReceiverInstance.TryGetComponent(out Collider collider))
                Object.DestroyImmediate(collider);

            MeshRenderer renderer = shadowReceiverInstance.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetShadowReceiverMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            preview.AddSingleGO(shadowReceiverInstance);
        }

        private Material GetShadowReceiverMaterial()
        {
            if (shadowReceiverMaterial != null)
                return shadowReceiverMaterial;

            Shader shader = GetLitPreviewShader();
            shadowReceiverMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = MontageSceneViewNavigation.PlaneColor
            };
            SetMaterialColor(shadowReceiverMaterial, MontageSceneViewNavigation.PlaneColor);
            ConfigureOpaqueReceiverMaterial(shadowReceiverMaterial);
            return shadowReceiverMaterial;
        }

        private static void ConfigureOpaqueReceiverMaterial(Material material)
        {
            if (material == null)
                return;

            material.renderQueue = (int)RenderQueue.Geometry;
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHABLEND_ON");
        }

        private static Shader GetLitPreviewShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
                return shader;

            shader = Shader.Find("HDRP/Lit");
            if (shader != null)
                return shader;

            shader = Shader.Find("Standard");
            if (shader != null)
                return shader;

            Shader fallback = Shader.Find("Diffuse");
            if (fallback != null)
                return fallback;

            return Shader.Find("Hidden/Internal-Colored");
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private void UpdateShadowReceiver(float groundPlaneY)
        {
            EnsureShadowReceiver();
            if (shadowReceiverInstance == null)
                return;

            bool visible = previewInstance != null
                           && hasBounds
                           && !viewportCamera.Is2DMode
                           && MontageSceneViewNavigation.LightEnabled
                           && MontageSceneViewNavigation.LightShadows;
            shadowReceiverInstance.SetActive(visible);
            if (visible && shadowReceiverMaterial != null)
                SetMaterialColor(shadowReceiverMaterial, MontageSceneViewNavigation.PlaneColor);
            if (!visible)
                return;
            shadowReceiverInstance.transform.position =
                new Vector3(renderBounds.center.x, groundPlaneY - 0.004f, renderBounds.center.z);
            shadowReceiverInstance.transform.rotation = Quaternion.identity;
            shadowReceiverInstance.transform.localScale = new Vector3(10f, 1f, 10f);
        }

        private static void ConfigureShadowCasting(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
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

        private void StoreInitialPreviewTransform()
        {
            if (previewInstance == null)
            {
                hasInitialPreviewTransform = false;
                return;
            }

            initialPreviewPosition = previewInstance.transform.position;
            initialPreviewRotation = previewInstance.transform.rotation;
            initialPreviewScale = previewInstance.transform.localScale;

            if (previewMotionRoot != null && previewMotionRoot != previewInstance.transform)
            {
                initialMotionRootLocalPosition = previewMotionRoot.localPosition;
                initialMotionRootLocalRotation = previewMotionRoot.localRotation;
                initialMotionRootLocalScale = previewMotionRoot.localScale;
            }
            else
            {
                initialMotionRootLocalPosition = Vector3.zero;
                initialMotionRootLocalRotation = Quaternion.identity;
                initialMotionRootLocalScale = Vector3.one;
            }

            hasInitialPreviewTransform = true;
        }

        private bool IsRootMotionPreviewEnabled() =>
            (boundContext?.Montage?.ApplyRootMotion ?? false);

        private void ApplyRootMotionPreviewTransform(MontageEditorContext context, float sampleTime)
        {
            if (context?.Montage == null || previewInstance == null || !hasInitialPreviewTransform)
                return;

            bool evaluated = MontageRootMotionPreviewUtility.TryEvaluate(
                context.Montage,
                sampleTime,
                out Vector3 rootPosition,
                out Quaternion rootRotation);

            if (!evaluated)
            {
                evaluated = rootMotionSampler.TryEvaluate(
                    previewInstance,
                    context.Montage,
                    sampleTime,
                    initialPreviewPosition,
                    initialPreviewRotation,
                    out rootPosition,
                    out rootRotation);
            }

            if (!evaluated)
                return;

            MontagePreviewSampling.TrySample(previewInstance, context, sampleTime);

            if (previewMotionRoot != null && previewMotionRoot != previewInstance.transform)
            {
                previewMotionRoot.localPosition = initialMotionRootLocalPosition;
                previewMotionRoot.localRotation = initialMotionRootLocalRotation;
                previewMotionRoot.localScale = initialMotionRootLocalScale;
            }

            previewInstance.transform.SetPositionAndRotation(
                initialPreviewPosition + initialPreviewRotation * rootPosition,
                initialPreviewRotation * rootRotation);
        }

        private void RevertTimelineElementPreviewTransform()
        {
            if (previewInstance == null || !HasTimelineElementTransform(previousPreviewTimelineElementEvaluation))
            {
                previousPreviewTimelineElementEvaluation = MontageTimelineElementEvaluation.Default;
                return;
            }

            Quaternion baseRotation = previewInstance.transform.rotation *
                                      Quaternion.Inverse(previousPreviewTimelineElementEvaluation.RotationOffset);
            previewInstance.transform.position -=
                baseRotation * previousPreviewTimelineElementEvaluation.PositionOffset;
            previewInstance.transform.rotation = baseRotation;
            previewInstance.transform.localScale -= previousPreviewTimelineElementEvaluation.ScaleOffset;
            previousPreviewTimelineElementEvaluation = MontageTimelineElementEvaluation.Default;
        }

        private void ApplyTimelineElementPreviewTransform(MontageEditorContext context)
        {
            if (context?.Montage == null || previewInstance == null)
                return;

            MontageTimelineElementEvaluation evaluation =
                MontageTimelineElementEvaluator.Evaluate(context.Montage, context.PlayheadTime);
            if (!HasTimelineElementTransform(evaluation))
            {
                previousPreviewTimelineElementEvaluation = MontageTimelineElementEvaluation.Default;
                return;
            }

            previewInstance.transform.position += previewInstance.transform.rotation * evaluation.PositionOffset;
            previewInstance.transform.rotation *= evaluation.RotationOffset;
            previewInstance.transform.localScale += evaluation.ScaleOffset;
            previousPreviewTimelineElementEvaluation = evaluation;
        }

        private static bool HasTimelineElementTransform(MontageTimelineElementEvaluation evaluation) =>
            evaluation.PositionOffset.sqrMagnitude > 0.0000001f
            || Quaternion.Angle(Quaternion.identity, evaluation.RotationOffset) > 0.0001f
            || evaluation.ScaleOffset.sqrMagnitude > 0.0000001f;

        private void StabilizePreviewTransform()
        {
            if (IsRootMotionPreviewEnabled())
                return;

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
            previewAnimationSampleTime = 0f;
            previousPreviewTimelineElementEvaluation = MontageTimelineElementEvaluation.Default;
            previewParticleSystems.Clear();
            previewVisualEffects.Clear();
            particleCacheBuffer.Clear();
            visualEffectCacheBuffer.Clear();
            hasBounds = false;
            hasPreviewHeightLock = false;
            hasInitialPreviewTransform = false;
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

        private void ClearProjectedMeshShadow()
        {
            if (projectedShadowInstance != null)
            {
                Object.DestroyImmediate(projectedShadowInstance);
                projectedShadowInstance = null;
            }

            if (projectedShadowMesh != null)
            {
                Object.DestroyImmediate(projectedShadowMesh);
                projectedShadowMesh = null;
            }

            if (bakedShadowMesh != null)
            {
                Object.DestroyImmediate(bakedShadowMesh);
                bakedShadowMesh = null;
            }

            if (projectedShadowMaterial != null)
            {
                Object.DestroyImmediate(projectedShadowMaterial);
                projectedShadowMaterial = null;
            }

            projectedShadowVertices.Clear();
            projectedShadowIndices.Clear();
            projectedShadowColors.Clear();
        }

        private void ClearShadowReceiver()
        {
            if (shadowReceiverInstance != null)
            {
                Object.DestroyImmediate(shadowReceiverInstance);
                shadowReceiverInstance = null;
            }

            if (shadowReceiverMaterial != null)
            {
                Object.DestroyImmediate(shadowReceiverMaterial);
                shadowReceiverMaterial = null;
            }
        }


        private void ClearPreviewSkybox()
        {
            if (previewSkyboxMaterial == null)
                return;

            Object.DestroyImmediate(previewSkyboxMaterial);
            previewSkyboxMaterial = null;
        }

        private sealed class ParticlePreviewEffect
        {
            public ParticlePreviewEffect(ParticleSystem particleSystem, double lastUpdateTime)
            {
                ParticleSystem = particleSystem;
                LastUpdateTime = lastUpdateTime;
            }

            public ParticleSystem ParticleSystem { get; }
            public double LastUpdateTime { get; set; }
        }

        private sealed class VisualEffectPreviewEffect
        {
            public VisualEffectPreviewEffect(VisualEffect visualEffect, double lastUpdateTime)
            {
                VisualEffect = visualEffect;
                LastUpdateTime = lastUpdateTime;
            }

            public VisualEffect VisualEffect { get; }
            public double LastUpdateTime { get; set; }
        }
    }
}





