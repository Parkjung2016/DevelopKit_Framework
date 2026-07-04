using System;
using UnityEngine;

namespace PJDev.UI
{
    [Flags]
    public enum SafeAreaEdges
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Bottom = 1 << 2,
        Top = 1 << 3,
        Horizontal = Left | Right,
        Vertical = Bottom | Top,
        All = Left | Right | Bottom | Top
    }

    /// <summary>
    /// <see cref="Screen.safeArea"/>에 맞춰 <see cref="RectTransform"/> 앵커를 갱신합니다.
    /// Device Simulator 미리보기를 위해 에디터에서도 동작합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("PJDev/Safe Area")]
    [ExecuteAlways]
    public sealed class SafeArea : MonoBehaviour
    {
        [SerializeField]
        private SafeAreaEdges controlEdges = SafeAreaEdges.All;

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public SafeAreaEdges ControlEdges
        {
            get => controlEdges;
            set
            {
                if (controlEdges == value)
                    return;

                controlEdges = value;
                Refresh();
            }
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Refresh();
        }

        private void Update()
        {
            UpdateRect();
        }

        /// <summary>Safe Area 기준으로 RectTransform을 즉시 갱신합니다.</summary>
        public void Refresh()
        {
            UpdateRect(force: true);
        }

        private void UpdateRect(bool force = false)
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            Rect safeArea = Screen.safeArea;
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            if (!force
                && safeArea.Equals(lastSafeArea)
                && lastScreenWidth == screenWidth
                && lastScreenHeight == screenHeight)
            {
                return;
            }

            ApplySafeArea(safeArea, screenWidth, screenHeight);

            lastSafeArea = safeArea;
            lastScreenWidth = screenWidth;
            lastScreenHeight = screenHeight;
        }

        private void ApplySafeArea(Rect safeArea, int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= screenWidth;
            anchorMin.y /= screenHeight;
            anchorMax.x /= screenWidth;
            anchorMax.y /= screenHeight;

            if (!HasEdge(SafeAreaEdges.Left))
                anchorMin.x = 0f;
            if (!HasEdge(SafeAreaEdges.Bottom))
                anchorMin.y = 0f;
            if (!HasEdge(SafeAreaEdges.Right))
                anchorMax.x = 1f;
            if (!HasEdge(SafeAreaEdges.Top))
                anchorMax.y = 1f;

            anchorMin = SanitizeAnchor(anchorMin, Vector2.zero);
            anchorMax = SanitizeAnchor(anchorMax, Vector2.one);

            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private bool HasEdge(SafeAreaEdges edge) => (controlEdges & edge) != 0;

        private static Vector2 SanitizeAnchor(Vector2 anchor, Vector2 fallback)
        {
            if (!IsFinite(anchor.x) || !IsFinite(anchor.y))
                return fallback;

            return anchor;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
