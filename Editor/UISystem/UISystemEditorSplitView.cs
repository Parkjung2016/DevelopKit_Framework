using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal enum UISystemEditorSplitOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>목록·상세 패널 사이 크기를 드래그로 조절하는 분할 뷰입니다.</summary>
    internal sealed class UISystemEditorSplitView : VisualElement
    {
        private const float SplitterThickness = 5f;
        private const float MinFirstPaneSize = 120f;
        private const float MinSecondPaneSize = 200f;
        private const float DefaultSecondPaneSize = 420f;

        private readonly UISystemEditorSplitOrientation orientation;
        private readonly VisualElement firstPane;
        private readonly VisualElement splitter;
        private readonly VisualElement secondPane;
        private bool isDragging;
        private int activePointerId = -1;
        private float secondPaneSize;

        public VisualElement FirstPane => firstPane;
        public VisualElement SecondPane => secondPane;

        public UISystemEditorSplitView(UISystemEditorSplitOrientation orientation, float initialSecondPaneSize = DefaultSecondPaneSize)
        {
            this.orientation = orientation;
            secondPaneSize = initialSecondPaneSize;

            style.flexGrow = 1;
            style.flexDirection = orientation == UISystemEditorSplitOrientation.Horizontal
                ? FlexDirection.Row
                : FlexDirection.Column;
            style.minHeight = 0;
            style.minWidth = 0;
            style.overflow = Overflow.Hidden;

            firstPane = CreatePane(isSecond: false);
            Add(firstPane);

            splitter = new VisualElement();
            splitter.style.flexShrink = 0;
            splitter.pickingMode = PickingMode.Position;
            splitter.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            if (orientation == UISystemEditorSplitOrientation.Horizontal)
                splitter.style.width = SplitterThickness;
            else
                splitter.style.height = SplitterThickness;

            splitter.RegisterCallback<PointerDownEvent>(OnSplitterPointerDown);
            splitter.RegisterCallback<PointerMoveEvent>(OnSplitterPointerMove);
            splitter.RegisterCallback<PointerUpEvent>(OnSplitterPointerUp);
            splitter.RegisterCallback<PointerCancelEvent>(OnSplitterPointerCancel);
            splitter.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            splitter.RegisterCallback<MouseEnterEvent>(_ =>
                splitter.style.backgroundColor = new Color(0.2f, 0.52f, 1f, 0.35f));
            splitter.RegisterCallback<MouseLeaveEvent>(_ =>
                splitter.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f));
            Add(splitter);

            secondPane = CreatePane(isSecond: true);
            Add(secondPane);

            RegisterCallback<GeometryChangedEvent>(_ => ClampSecondPaneSize());
            ApplySecondPaneSize(secondPaneSize);
        }

        private VisualElement CreatePane(bool isSecond)
        {
            var pane = new VisualElement();
            pane.style.flexDirection = FlexDirection.Column;
            pane.style.overflow = Overflow.Hidden;
            pane.style.minHeight = 0;
            pane.style.minWidth = 0;

            if (isSecond)
            {
                pane.style.flexShrink = 1;
                pane.style.minWidth = MinSecondPaneSize;
                pane.style.minHeight = MinSecondPaneSize;
            }
            else
            {
                pane.style.flexGrow = 1;
                pane.style.flexShrink = 1;
                pane.style.minWidth = MinFirstPaneSize;
                pane.style.minHeight = MinFirstPaneSize;
            }

            return pane;
        }

        private void OnSplitterPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || isDragging)
                return;

            isDragging = true;
            activePointerId = evt.pointerId;
            splitter.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnSplitterPointerMove(PointerMoveEvent evt)
        {
            if (!isDragging || evt.pointerId != activePointerId)
                return;

            if (!splitter.HasPointerCapture(evt.pointerId))
                return;

            ApplySecondPaneSizeFromPointer(evt.position);
            evt.StopPropagation();
        }

        private void OnSplitterPointerUp(PointerUpEvent evt)
        {
            if (!isDragging || evt.pointerId != activePointerId)
                return;

            EndDrag(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnSplitterPointerCancel(PointerCancelEvent evt)
        {
            if (!isDragging || evt.pointerId != activePointerId)
                return;

            EndDrag(evt.pointerId);
            evt.StopPropagation();
        }

        private void EndDrag(int pointerId = -1)
        {
            if (!isDragging)
                return;

            int id = pointerId >= 0 ? pointerId : activePointerId;
            if (id >= 0 && splitter.HasPointerCapture(id))
                splitter.ReleasePointer(id);

            isDragging = false;
            activePointerId = -1;
        }

        private void ApplySecondPaneSizeFromPointer(Vector2 panelPosition)
        {
            Rect bounds = worldBound;
            if (orientation == UISystemEditorSplitOrientation.Horizontal)
            {
                if (bounds.width <= 0f)
                    return;

                ApplySecondPaneSize(bounds.xMax - panelPosition.x);
                return;
            }

            if (bounds.height <= 0f)
                return;

            ApplySecondPaneSize(bounds.yMax - panelPosition.y);
        }

        private void ApplySecondPaneSize(float desiredSecondSize)
        {
            if (orientation == UISystemEditorSplitOrientation.Horizontal)
            {
                float available = resolvedStyle.width;
                if (float.IsNaN(available) || available <= 0f)
                    available = worldBound.width;

                if (available <= 0f)
                    return;

                float maxSecond = available - SplitterThickness - MinFirstPaneSize;
                float minSecond = Mathf.Min(MinSecondPaneSize, Mathf.Max(80f, maxSecond));
                maxSecond = Mathf.Max(minSecond, maxSecond);

                secondPaneSize = Mathf.Clamp(desiredSecondSize, minSecond, maxSecond);
                secondPane.style.width = secondPaneSize;
                secondPane.style.maxWidth = maxSecond;
                secondPane.style.height = StyleKeyword.Auto;
                return;
            }

            float availableHeight = resolvedStyle.height;
            if (float.IsNaN(availableHeight) || availableHeight <= 0f)
                availableHeight = worldBound.height;

            if (availableHeight <= 0f)
                return;

            float maxSecondHeight = availableHeight - SplitterThickness - MinFirstPaneSize;
            float minSecondHeight = Mathf.Min(MinSecondPaneSize, Mathf.Max(80f, maxSecondHeight));
            maxSecondHeight = Mathf.Max(minSecondHeight, maxSecondHeight);

            secondPaneSize = Mathf.Clamp(desiredSecondSize, minSecondHeight, maxSecondHeight);
            secondPane.style.height = secondPaneSize;
            secondPane.style.maxHeight = maxSecondHeight;
            secondPane.style.width = StyleKeyword.Auto;
        }

        private void ClampSecondPaneSize() => ApplySecondPaneSize(secondPaneSize);
    }
}
