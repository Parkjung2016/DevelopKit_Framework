using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>좌·우 패널 사이 폭을 조절할 수 있는 분할 뷰입니다.</summary>
    internal sealed class GameplayTagSplitView : VisualElement
    {
        private const float SplitterWidth = 5f;
        private const float MinLeftWidth = 120f;
        private const float MinRightWidth = 200f;
        private const float DefaultRightWidth = 420f;

        private readonly VisualElement leftPane;
        private readonly VisualElement splitter;
        private readonly VisualElement rightPane;
        private bool isDragging;
        private int activePointerId = -1;
        private float rightWidth = DefaultRightWidth;

        public VisualElement LeftPane => leftPane;
        public VisualElement RightPane => rightPane;

        public GameplayTagSplitView(float initialRightWidth = DefaultRightWidth)
        {
            rightWidth = initialRightWidth;
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Row;
            style.minHeight = 0;
            style.minWidth = 0;
            style.overflow = Overflow.Hidden;

            leftPane = new VisualElement();
            leftPane.AddToClassList(GameplayTagEditorStyles.SplitLeftClass);
            leftPane.style.flexGrow = 1;
            leftPane.style.flexShrink = 1;
            leftPane.style.minWidth = MinLeftWidth;
            leftPane.style.minHeight = 0;
            leftPane.style.flexDirection = FlexDirection.Column;
            leftPane.style.overflow = Overflow.Hidden;
            Add(leftPane);

            splitter = new VisualElement();
            splitter.AddToClassList(GameplayTagEditorStyles.SplitterClass);
            splitter.style.flexShrink = 0;
            splitter.pickingMode = PickingMode.Position;
            splitter.RegisterCallback<PointerDownEvent>(OnSplitterPointerDown);
            splitter.RegisterCallback<PointerMoveEvent>(OnSplitterPointerMove);
            splitter.RegisterCallback<PointerUpEvent>(OnSplitterPointerUp);
            splitter.RegisterCallback<PointerCancelEvent>(OnSplitterPointerCancel);
            splitter.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            Add(splitter);

            rightPane = new VisualElement();
            rightPane.AddToClassList(GameplayTagEditorStyles.SplitRightClass);
            rightPane.style.flexShrink = 1;
            rightPane.style.minWidth = MinRightWidth;
            rightPane.style.minHeight = 0;
            rightPane.style.flexDirection = FlexDirection.Column;
            rightPane.style.overflow = Overflow.Hidden;
            Add(rightPane);

            RegisterCallback<GeometryChangedEvent>(_ => ClampPaneWidths());
            ApplyRightWidth(rightWidth);
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

            ApplyRightWidthFromMouse(evt.position.x);
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

        private void ApplyRightWidthFromMouse(float mousePanelX)
        {
            Rect bounds = worldBound;
            if (bounds.width <= 0f)
                return;

            float newRightWidth = bounds.xMax - mousePanelX;
            ApplyRightWidth(newRightWidth);
        }

        private void ApplyRightWidth(float desiredRightWidth)
        {
            float available = resolvedStyle.width;
            if (float.IsNaN(available) || available <= 0f)
                available = worldBound.width;

            if (available <= 0f)
                return;

            float maxRight = available - SplitterWidth - MinLeftWidth;
            float minRight = Mathf.Min(MinRightWidth, Mathf.Max(80f, maxRight));
            maxRight = Mathf.Max(minRight, maxRight);

            rightWidth = Mathf.Clamp(desiredRightWidth, minRight, maxRight);
            rightPane.style.width = rightWidth;
            rightPane.style.maxWidth = maxRight;
        }

        private void ClampPaneWidths()
        {
            ApplyRightWidth(rightWidth);
        }
    }
}
