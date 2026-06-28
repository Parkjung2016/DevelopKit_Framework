using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>Canvas 묶음별 루트와 레이어 RectTransform을 관리합니다.</summary>
    public sealed class UILayerRoots : MonoBehaviour
    {
        private readonly Dictionary<string, RectTransform> rootsByLayerId = new();
        private readonly Dictionary<string, Canvas> canvasesByGroupId = new();

        public RectTransform GetRoot(string layerId, UILayerRegistry registry)
        {
            if (string.IsNullOrEmpty(layerId))
                return null;

            if (TryGetStoredRoot(layerId, out RectTransform root))
                return root;

            if (registry != null
                && string.Equals(layerId, UILayers.Screen, StringComparison.Ordinal)
                && !registry.TryGet(UILayers.Screen, out _)
                && TryGetStoredRoot(registry.ScreenLayerId, out root))
            {
                return root;
            }

            return null;
        }

        public Canvas GetCanvas(string groupId) =>
            canvasesByGroupId.TryGetValue(groupId, out Canvas canvas) ? canvas : null;

        public Canvas GetCanvas(UICanvasGroup group) => GetCanvas(UICanvasGroupUtility.EnumToId(group));

        public void EnsureDefaults(UILayerRegistry registry)
        {
            if (registry == null)
                return;

            IReadOnlyList<string> groupIds = registry.AllCanvasGroupIds;
            for (int i = 0; i < groupIds.Count; i++)
            {
                string groupId = groupIds[i];
                if (!registry.TryGetCanvasGroup(groupId, out UICanvasGroupDefinition definition))
                    continue;

                EnsureGroupCanvas(groupId, definition.CanvasName, definition.SortingOrder);
            }

            IReadOnlyList<string> layerIds = registry.AllLayerIds;
            for (int i = 0; i < layerIds.Count; i++)
            {
                string layerId = layerIds[i];
                if (!registry.TryGet(layerId, out UILayerDefinition definition))
                    continue;

                string groupId = definition.CanvasGroupId;
                if (!canvasesByGroupId.TryGetValue(groupId, out Canvas canvas))
                    continue;

                rootsByLayerId[layerId] = GetOrCreateLayerRoot(canvas.transform, definition.RootName);
            }
        }

        private bool TryGetStoredRoot(string layerId, out RectTransform root)
        {
            if (rootsByLayerId.TryGetValue(layerId, out root) && root != null)
                return true;

            root = null;
            return false;
        }

        public void SetRaycasterEnabled(string groupId, bool enabled)
        {
            Canvas canvas = GetCanvas(groupId);
            if (canvas == null)
                return;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = enabled;
        }

        public void SetRaycasterEnabled(UICanvasGroup group, bool enabled) =>
            SetRaycasterEnabled(UICanvasGroupUtility.EnumToId(group), enabled);

        private void EnsureGroupCanvas(string groupId, string canvasName, int sortingOrder)
        {
            if (canvasesByGroupId.ContainsKey(groupId))
                return;

            Transform existing = transform.Find(canvasName);
            if (existing != null)
            {
                Canvas existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null)
                {
                    canvasesByGroupId[groupId] = existingCanvas;
                    return;
                }
            }

            GameObject canvasObject = new GameObject(
                canvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            StretchFull(rect);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasesByGroupId[groupId] = canvas;
        }

        private static RectTransform GetOrCreateLayerRoot(Transform canvasTransform, string rootName)
        {
            if (string.IsNullOrEmpty(rootName))
                return null;

            Transform existing = canvasTransform.Find(rootName);
            if (existing != null)
            {
                if (existing is RectTransform existingRect)
                    return existingRect;

                CDebug.LogWarning(
                    $"UI layer root '{rootName}' exists but is not a RectTransform. Creating '{rootName}UI' instead.");
                rootName = $"{rootName}UI";
            }

            GameObject rootObject = new GameObject(rootName, typeof(RectTransform));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            StretchFull(rect);
            return rect;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
