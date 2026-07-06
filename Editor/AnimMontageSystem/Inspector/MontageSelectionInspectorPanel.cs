using System;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageSelectionInspectorPanel : VisualElement
    {
        private readonly MontageEditorContext context;
        private readonly ScrollView scrollView = new(ScrollViewMode.Vertical);
        private readonly VisualElement host = new();

        public MontageSelectionInspectorPanel(MontageEditorContext context)
        {
            this.context = context;
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.minHeight = 0;
            style.overflow = Overflow.Hidden;
            style.flexDirection = FlexDirection.Column;

            Add(MontageEditorLayoutHelper.CreatePanelHeader("Inspector"));

            scrollView.AddToClassList(AnimMontageEditorStyles.InspectorHostClass);
            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.minHeight = 0;
            scrollView.Add(host);
            Add(scrollView);

            context.SelectionChanged += Rebuild;
            Rebuild();
        }

        private void Rebuild()
        {
            host.Clear();
            UnityEngine.Object selected = context.SelectedObject ?? context.Montage;
            if (selected == null)
            {
                host.Add(new Label("Select a montage or notify.")
                {
                    style = { whiteSpace = WhiteSpace.Normal }
                });
                return;
            }

            var editor = UnityEditor.Editor.CreateEditor(selected);
            var inspector = new InspectorElement(editor);
            inspector.style.flexGrow = 1;
            inspector.TrackSerializedObjectValue(editor.serializedObject, _ => context.NotifyExternalChange());
            host.Add(inspector);
        }
    }
}
