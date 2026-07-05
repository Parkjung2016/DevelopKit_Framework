using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontagePreviewViewportPanel : VisualElement
    {
        private readonly IMGUIContainer imguiHost;
        private readonly Action<Rect, Action> drawPreview;
        private double lastRepaintTime;

        public MontagePreviewViewportPanel(Action<Rect, Action> drawPreview)
        {
            this.drawPreview = drawPreview;
            MontageEditorLayoutHelper.ConfigurePane(this);
            style.flexDirection = FlexDirection.Column;

            Add(MontageEditorLayoutHelper.CreatePanelHeader("Viewport"));

            imguiHost = new IMGUIContainer(OnDraw);
            imguiHost.focusable = true;
            imguiHost.RegisterCallback<PointerDownEvent>(_ => imguiHost.Focus());
            imguiHost.RegisterCallback<KeyDownEvent>(OnViewportKeyDown, TrickleDown.TrickleDown);
            imguiHost.AddToClassList(AnimMontageEditorStyles.PreviewHostClass);
            imguiHost.style.flexGrow = 1;
            imguiHost.style.flexShrink = 1;
            imguiHost.style.flexBasis = 0;
            imguiHost.style.minHeight = 180;
            Add(imguiHost);

            RegisterCallback<GeometryChangedEvent>(_ => RequestRepaint());
            RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.update += OnEditorUpdate);
            RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.update -= OnEditorUpdate);
        }

        private void OnEditorUpdate()
        {
            if (!imguiHost.enabledInHierarchy)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now - lastRepaintTime < 0.05)
                return;

            lastRepaintTime = now;
            RequestRepaint();
        }

        private void OnDraw()
        {
            Rect rect = imguiHost.contentRect;
            if (rect.width < 1f || rect.height < 1f)
                return;

            drawPreview?.Invoke(rect, RequestRepaint);
        }

        public void RequestRepaint() => imguiHost.MarkDirtyRepaint();

        private void OnViewportKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Space)
                return;

            imguiHost.MarkDirtyRepaint();
        }
    }
}
