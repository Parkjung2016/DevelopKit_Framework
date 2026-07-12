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
            RegisterCallback<PointerEnterEvent>(_ => MontageViewportInput.CancelInteraction(), TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(_ => MontageViewportInput.CancelInteraction(), TrickleDown.TrickleDown);
            RegisterCallback<FocusInEvent>(_ => MontageViewportInput.CancelInteraction(), TrickleDown.TrickleDown);
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
            if (selected is AnimMontageSO && TryBuildMontageInspector())
                return;

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

            if (TryBuildMultiSelectionInspector())
                return true;

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
                    GetManagedReferenceTitle("Anim Notify", "notifies", context.SelectedNotifyIndex, "notify"),
                    "notifies",
                    context.SelectedNotifyIndex,
                    "notify",
                    "time",
                    "customColor");

            if (context.SelectedNotifyStateIndex >= 0)
                return BuildArrayElementInspector(
                    GetManagedReferenceTitle("Anim Notify State", "notifyStates", context.SelectedNotifyStateIndex, "notifyState"),
                    "notifyStates",
                    context.SelectedNotifyStateIndex,
                    "notifyState",
                    "startTime",
                    "endTime",
                    "customColor");

            if (context.SelectedCustomElementIndex >= 0)
                return BuildArrayElementInspector(
                    GetManagedReferenceTitle("Custom Element", "customElements", context.SelectedCustomElementIndex, "element"),
                    "customElements",
                    context.SelectedCustomElementIndex,
                    "element",
                    "startTime",
                    "endTime",
                    "customColor");

            return false;
        }

        private bool TryBuildMultiSelectionInspector()
        {
            int segmentCount = context.SelectedSegmentIndices.Count;
            int notifyCount = context.SelectedNotifyIndices.Count;
            int stateCount = context.SelectedNotifyStateIndices.Count;
            int customCount = context.SelectedCustomElementIndices.Count;
            int trackCount = context.SelectedTimelineTrackKeys.Count;
            int totalCount = segmentCount + notifyCount + stateCount + customCount + trackCount;
            if (totalCount <= 1)
                return false;

            host.Add(new Label("Multi Selection")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            });

            host.Add(new Label($"{totalCount} timeline items selected.")
            {
                style =
                {
                    marginBottom = 8f,
                    whiteSpace = WhiteSpace.Normal,
                    color = new Color(0.78f, 0.82f, 0.9f, 0.9f)
                }
            });

            AddSelectionCountLabel("Segments", segmentCount);
            AddSelectionCountLabel("Notifies", notifyCount);
            AddSelectionCountLabel("Notify States", stateCount);
            AddSelectionCountLabel("Custom Elements", customCount);
            AddSelectionCountLabel("Tracks", trackCount);
            return true;
        }

        private void AddSelectionCountLabel(string label, int count)
        {
            if (count <= 0)
                return;

            host.Add(new Label($"{label}: {count}")
            {
                style =
                {
                    marginBottom = 2f,
                    color = new Color(1f, 1f, 1f, 0.82f)
                }
            });
        }

        private string GetManagedReferenceTitle(string fallback, string arrayPropertyName, int index, string managedReferenceName)
        {
            SerializedProperty array = boundObject.FindProperty(arrayPropertyName);
            if (array == null || index < 0 || index >= array.arraySize)
                return fallback;

            SerializedProperty property = array.GetArrayElementAtIndex(index).FindPropertyRelative(managedReferenceName);
            object value = property?.managedReferenceValue;
            return value switch
            {
                AnimNotify notify => $"{fallback}: {notify.DisplayName}",
                AnimNotifyState state => $"{fallback}: {state.DisplayName}",
                MontageTimelineElement element => $"{fallback}: {element.DisplayName}",
                _ => fallback
            };
        }

        private bool TryBuildMontageInspector()
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return false;

            boundObject = new SerializedObject(montage);
            host.Add(new Label("Montage")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            });

            AddProperty("rateScale");
            AddProperty("applyRootMotion");

            host.Add(new Label(
                $"Segments: {montage.Segments.Count} | Notifies: {montage.Notifies.Count} | States: {montage.NotifyStates.Count}")
            {
                style =
                {
                    marginTop = 8f,
                    whiteSpace = WhiteSpace.Normal,
                    color = new Color(0.78f, 0.82f, 0.9f, 0.82f)
                }
            });

            host.Bind(boundObject);
            return true;
        }

        private void AddProperty(string propertyName)
        {
            SerializedProperty property = boundObject.FindProperty(propertyName);
            if (property != null)
                host.Add(new PropertyField(property));
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
