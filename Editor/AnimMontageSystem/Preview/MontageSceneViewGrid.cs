using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageSceneViewGrid
    {
        private const float MinimumGridStep = 0.05f;
        private const int LinesPerDirection = 80;
        private const int MajorLineFrequency = 5;
        private const float Fixed3DGridHalfSize = 5f;
        private const float Fixed3DGridStep = 0.5f;

        private static readonly Color MinorLineColor = new(0.46f, 0.46f, 0.46f, 0.34f);
        private static readonly Color MajorLineColor = new(0.58f, 0.58f, 0.58f, 0.46f);
        private static readonly Color AxisXColor = new(0.86f, 0.25f, 0.22f, 0.72f);
        private static readonly Color AxisYColor = new(0.36f, 0.72f, 0.28f, 0.72f);
        private static readonly Color AxisZColor = new(0.28f, 0.52f, 0.95f, 0.72f);

        private static readonly List<Vector3> Vertices = new();
        private static readonly List<Color> Colors = new();
        private static readonly List<int> Indices = new();

        private static Material lineMaterial;

        public static GameObject CreatePreviewGrid()
        {
            GameObject gridObject = new("Montage Preview Grid")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Mesh mesh = new()
            {
                name = "Montage Preview Grid Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            gridObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = gridObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return gridObject;
        }

        public static void DrawPreview(
            Camera camera,
            GameObject gridObject,
            MontageViewportCamera viewportCamera,
            bool useFrontGrid,
            float groundPlaneY)
        {
            if (camera == null || viewportCamera == null)
                return;

            UpdateGrid(gridObject, viewportCamera, useFrontGrid, groundPlaneY);
            camera.Render();
        }

        public static void UpdateGrid(
            GameObject gridObject,
            MontageViewportCamera viewportCamera,
            bool useFrontGrid,
            float groundPlaneY)
        {
            if (gridObject == null || viewportCamera == null)
                return;

            if (!gridObject.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                return;

            float step = GetNiceStep(Mathf.Max(viewportCamera.Size, 1f) / 12f);
            Vector3 pivot = viewportCamera.Pivot;
            Vertices.Clear();
            Colors.Clear();
            Indices.Clear();

            if (useFrontGrid || viewportCamera.Is2DMode)
            {
                BuildPlaneGrid(
                    Snap(pivot.x, step),
                    Snap(pivot.y, step),
                    step,
                    Axis.X,
                    Axis.Y,
                    0f);
            }
            else
            {
                BuildFixedPlaneGrid(
                    Axis.X,
                    Axis.Z,
                    groundPlaneY);
            }

            Mesh mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(Vertices);
            mesh.SetColors(Colors);
            mesh.SetIndices(Indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        private static void BuildPlaneGrid(float centerA, float centerB, float step, Axis axisA, Axis axisB, float planeOffset)
        {
            int centerLineA = Mathf.RoundToInt(centerA / step);
            int centerLineB = Mathf.RoundToInt(centerB / step);
            float minA = (centerLineA - LinesPerDirection) * step;
            float maxA = (centerLineA + LinesPerDirection) * step;
            float minB = (centerLineB - LinesPerDirection) * step;
            float maxB = (centerLineB + LinesPerDirection) * step;

            for (int i = -LinesPerDirection; i <= LinesPerDirection; i++)
            {
                int lineIndexA = centerLineA + i;
                float a = lineIndexA * step;
                AddLine(
                    BuildPoint(a, minB, axisA, axisB, planeOffset),
                    BuildPoint(a, maxB, axisA, axisB, planeOffset),
                    GetLineColor(lineIndexA, axisA));

                int lineIndexB = centerLineB + i;
                float b = lineIndexB * step;
                AddLine(
                    BuildPoint(minA, b, axisA, axisB, planeOffset),
                    BuildPoint(maxA, b, axisA, axisB, planeOffset),
                    GetLineColor(lineIndexB, axisB));
            }
        }

        private static void BuildFixedPlaneGrid(Axis axisA, Axis axisB, float planeOffset)
        {
            int lineCount = Mathf.RoundToInt(Fixed3DGridHalfSize / Fixed3DGridStep);
            float min = -Fixed3DGridHalfSize;
            float max = Fixed3DGridHalfSize;

            for (int i = -lineCount; i <= lineCount; i++)
            {
                float position = i * Fixed3DGridStep;
                AddLine(
                    BuildPoint(position, min, axisA, axisB, planeOffset),
                    BuildPoint(position, max, axisA, axisB, planeOffset),
                    GetLineColor(i, axisA));

                AddLine(
                    BuildPoint(min, position, axisA, axisB, planeOffset),
                    BuildPoint(max, position, axisA, axisB, planeOffset),
                    GetLineColor(i, axisB));
            }
        }

        private static void AddLine(Vector3 from, Vector3 to, Color color)
        {
            int index = Vertices.Count;
            Vertices.Add(from);
            Vertices.Add(to);
            Colors.Add(color);
            Colors.Add(color);
            Indices.Add(index);
            Indices.Add(index + 1);
        }

        private static Color GetLineColor(int lineIndex, Axis axis)
        {
            if (lineIndex == 0)
            {
                return axis switch
                {
                    Axis.X => AxisXColor,
                    Axis.Y => AxisYColor,
                    Axis.Z => AxisZColor,
                    _ => MajorLineColor,
                };
            }

            return lineIndex % MajorLineFrequency == 0 ? MajorLineColor : MinorLineColor;
        }

        private static Vector3 BuildPoint(float a, float b, Axis axisA, Axis axisB, float planeOffset)
        {
            Vector3 point = Vector3.zero;
            point[(int)axisA] = a;
            point[(int)axisB] = b;
            point[(int)GetNormalAxis(axisA, axisB)] = planeOffset;
            return point;
        }

        private static Axis GetNormalAxis(Axis axisA, Axis axisB)
        {
            if (axisA != Axis.X && axisB != Axis.X)
                return Axis.X;

            if (axisA != Axis.Y && axisB != Axis.Y)
                return Axis.Y;

            return Axis.Z;
        }

        private static float Snap(float value, float step) =>
            Mathf.Round(value / step) * step;

        private static float GetNiceStep(float targetStep)
        {
            float exponent = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(targetStep, MinimumGridStep))));
            float normalized = targetStep / exponent;

            if (normalized >= 5f)
                return 5f * exponent;

            if (normalized >= 2f)
                return 2f * exponent;

            return exponent;
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial != null)
                return lineMaterial;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return null;

            lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            return lineMaterial;
        }

        private enum Axis
        {
            X = 0,
            Y = 1,
            Z = 2,
        }
    }

    internal static class MontagePreviewSceneGizmos
    {
        private const string ShadowName = "Montage Preview Shadow";
        private const string LinesName = "Montage Preview Avatar Gizmos";
        private const int ShadowSegments = 48;

        private static readonly Color ShadowCenterColor = new(0f, 0f, 0f, 0.28f);
        private static readonly Color ShadowEdgeColor = new(0f, 0f, 0f, 0f);
        private static readonly Color RootDirectionColor = new(1f, 0.62f, 0.08f, 1f);
        private static readonly Color PivotColor = new(1f, 0.9f, 0.24f, 1f);
        private static readonly Color MassCenterColor = new(0.2f, 0.82f, 1f, 1f);

        private static readonly List<Vector3> ShadowVertices = new();
        private static readonly List<Color> ShadowColors = new();
        private static readonly List<int> ShadowIndices = new();
        private static readonly List<Vector3> LineVertices = new();
        private static readonly List<Color> LineColors = new();
        private static readonly List<int> LineIndices = new();

        private static Material transparentMaterial;
        private static Material lineMaterial;

        public static GameObject Create()
        {
            GameObject root = new("Montage Preview Scene Gizmos")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            CreateMeshObject(ShadowName, root.transform, GetTransparentMaterial());
            CreateMeshObject(LinesName, root.transform, GetLineMaterial());
            return root;
        }

        public static void Update(
            GameObject root,
            GameObject previewInstance,
            Bounds bounds,
            bool hasBounds,
            float groundPlaneY,
            bool useFrontGrid)
        {
            if (root == null)
                return;

            Transform shadow = root.transform.Find(ShadowName);
            Transform lines = root.transform.Find(LinesName);
            bool showSceneGizmos = previewInstance != null && hasBounds;

            if (shadow != null)
            {
                shadow.gameObject.SetActive(showSceneGizmos && !useFrontGrid);
                if (shadow.gameObject.activeSelf)
                    UpdateShadow(shadow.gameObject, bounds, groundPlaneY);
            }

            if (lines != null)
            {
                lines.gameObject.SetActive(showSceneGizmos);
                if (lines.gameObject.activeSelf)
                    UpdateLines(lines.gameObject, previewInstance, bounds, groundPlaneY, useFrontGrid);
            }
        }

        public static void Destroy(GameObject root)
        {
            if (root == null)
                return;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] != null && filters[i].sharedMesh != null)
                    Object.DestroyImmediate(filters[i].sharedMesh);
            }

            Object.DestroyImmediate(root);
        }

        private static void CreateMeshObject(string name, Transform parent, Material material)
        {
            GameObject meshObject = new(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            meshObject.transform.SetParent(parent, false);

            Mesh mesh = new()
            {
                name = name + " Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static void UpdateShadow(GameObject shadowObject, Bounds bounds, float groundPlaneY)
        {
            if (!shadowObject.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                return;

            float radiusX = Mathf.Max(bounds.extents.x * 1.25f, 0.25f);
            float radiusZ = Mathf.Max(bounds.extents.z * 1.25f, 0.25f);
            Vector3 center = new(bounds.center.x, groundPlaneY + 0.004f, bounds.center.z);

            ShadowVertices.Clear();
            ShadowColors.Clear();
            ShadowIndices.Clear();

            ShadowVertices.Add(center);
            ShadowColors.Add(ShadowCenterColor);

            for (int i = 0; i <= ShadowSegments; i++)
            {
                float angle = (float)i / ShadowSegments * Mathf.PI * 2f;
                ShadowVertices.Add(center + new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ));
                ShadowColors.Add(ShadowEdgeColor);
            }

            for (int i = 1; i <= ShadowSegments; i++)
            {
                ShadowIndices.Add(0);
                ShadowIndices.Add(i);
                ShadowIndices.Add(i + 1);
            }

            Mesh mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(ShadowVertices);
            mesh.SetColors(ShadowColors);
            mesh.SetIndices(ShadowIndices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
        }

        private static void UpdateLines(
            GameObject linesObject,
            GameObject previewInstance,
            Bounds bounds,
            float groundPlaneY,
            bool useFrontGrid)
        {
            if (!linesObject.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                return;

            float size = Mathf.Max(bounds.extents.magnitude, 0.25f);
            float markerSize = Mathf.Clamp(size * 0.09f, 0.06f, 0.32f);
            Transform avatarRoot = GetAvatarRoot(previewInstance);
            Vector3 pivot = avatarRoot.position;
            Vector3 massCenter = bounds.center;

            LineVertices.Clear();
            LineColors.Clear();
            LineIndices.Clear();

            AddCross(pivot, markerSize, PivotColor);
            AddCross(massCenter, markerSize, MassCenterColor);
            AddLine(new Vector3(pivot.x, groundPlaneY, pivot.z), pivot, PivotColor);
            AddLine(new Vector3(massCenter.x, groundPlaneY, massCenter.z), massCenter, MassCenterColor);

            if (!useFrontGrid)
                AddRootDirection(avatarRoot, groundPlaneY, Mathf.Clamp(size * 0.55f, 0.5f, 3.2f));

            Mesh mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(LineVertices);
            mesh.SetColors(LineColors);
            mesh.SetIndices(LineIndices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        private static Transform GetAvatarRoot(GameObject previewInstance)
        {
            Animator animator = previewInstance.GetComponentInChildren<Animator>();
            return animator != null ? animator.transform : previewInstance.transform;
        }

        private static void AddRootDirection(Transform root, float groundPlaneY, float length)
        {
            Vector3 origin = new(root.position.x, groundPlaneY + 0.018f, root.position.z);
            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();
            Vector3 tip = origin + forward * length;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float shaftHalfWidth = Mathf.Clamp(length * 0.035f, 0.025f, 0.09f);
            float ringRadius = Mathf.Clamp(length * 0.08f, 0.06f, 0.18f);
            float headLength = length * 0.34f;
            float headWidth = length * 0.2f;
            Vector3 shaftEnd = tip - forward * headLength * 0.45f;

            AddGroundCircle(origin, ringRadius, RootDirectionColor, 20);
            AddLine(origin, tip, RootDirectionColor);
            AddLine(origin + right * shaftHalfWidth, shaftEnd + right * shaftHalfWidth, RootDirectionColor);
            AddLine(origin - right * shaftHalfWidth, shaftEnd - right * shaftHalfWidth, RootDirectionColor);
            AddLine(tip, tip - forward * headLength + right * headWidth, RootDirectionColor);
            AddLine(tip, tip - forward * headLength - right * headWidth, RootDirectionColor);
            AddLine(tip - forward * headLength + right * headWidth, tip - forward * headLength * 0.55f, RootDirectionColor);
            AddLine(tip - forward * headLength - right * headWidth, tip - forward * headLength * 0.55f, RootDirectionColor);
        }

        private static void AddCross(Vector3 center, float size, Color color)
        {
            AddLine(center - Vector3.right * size, center + Vector3.right * size, color);
            AddLine(center - Vector3.up * size, center + Vector3.up * size, color);
            AddLine(center - Vector3.forward * size, center + Vector3.forward * size, color);
        }

        private static void AddGroundCircle(Vector3 center, float radius, Color color, int segments)
        {
            if (segments < 3)
                return;

            Vector3 previous = center + Vector3.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                AddLine(previous, next, color);
                previous = next;
            }
        }

        private static void AddLine(Vector3 from, Vector3 to, Color color)
        {
            int index = LineVertices.Count;
            LineVertices.Add(from);
            LineVertices.Add(to);
            LineColors.Add(color);
            LineColors.Add(color);
            LineIndices.Add(index);
            LineIndices.Add(index + 1);
        }

        private static Material GetTransparentMaterial()
        {
            if (transparentMaterial != null)
                return transparentMaterial;

            transparentMaterial = CreateInternalColoredMaterial(UnityEngine.Rendering.CompareFunction.LessEqual);
            return transparentMaterial;
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial != null)
                return lineMaterial;

            lineMaterial = CreateInternalColoredMaterial(UnityEngine.Rendering.CompareFunction.Always);
            return lineMaterial;
        }

        private static Material CreateInternalColoredMaterial(UnityEngine.Rendering.CompareFunction zTest)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return null;

            Material material = new(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)zTest);
            return material;
        }
    }
}
