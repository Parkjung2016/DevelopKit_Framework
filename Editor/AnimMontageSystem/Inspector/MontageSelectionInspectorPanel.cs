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
        private SerializedObject boundObject;

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
            host.RegisterCallback<SerializedPropertyChangeEvent>(_ => context.NotifyExternalChange());
            Rebuild();
        }

        private void Rebuild()
        {
            host.Unbind();
            host.Clear();
            boundObject = null;
            if (TryBuildTimelineElementInspector())
                return;

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
            host.Add(inspector);
        }

        private bool TryBuildTimelineElementInspector()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return false;

            boundObject = new SerializedObject(montage);
            if (context.SelectedSegmentIndex >= 0)
                return BuildArrayElementInspector(
                    "Animation Segment",
                    "segments",
                    context.SelectedSegmentIndex,
                    "sectionName",
                    "clip",
                    "startTime",
                    "clipStartTime",
                    "clipEndTime",
                    "playRate",
                    "customColor");

            if (context.SelectedNotifyIndex >= 0)
                return BuildArrayElementInspector(
                    "Anim Notify",
                    "notifies",
                    context.SelectedNotifyIndex,
                    "notify",
                    "time",
                    "customColor");

            if (context.SelectedNotifyStateIndex >= 0)
                return BuildArrayElementInspector(
                    "Anim Notify State",
                    "notifyStates",
                    context.SelectedNotifyStateIndex,
                    "notifyState",
                    "startTime",
                    "endTime",
                    "customColor");

            if (context.SelectedCustomElementIndex >= 0)
                return BuildArrayElementInspector(
                    "Custom Timeline Element",
                    "customElements",
                    context.SelectedCustomElementIndex,
                    "element",
                    "startTime",
                    "endTime",
                    "trackId",
                    "customColor");

            return false;
        }

        private bool BuildArrayElementInspector(string title, string arrayPropertyName, int index, params string[] propertyNames)
        {
            SerializedProperty array = boundObject.FindProperty(arrayPropertyName);
            if (array == null || index < 0 || index >= array.arraySize)
                return false;

            host.Add(new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            });

            SerializedProperty element = array.GetArrayElementAtIndex(index);
            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = element.FindPropertyRelative(propertyNames[i]);
                if (property != null)
                    host.Add(new PropertyField(property));
            }

            host.Bind(boundObject);
            return true;
        }
    }
}
