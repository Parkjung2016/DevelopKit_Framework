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

        private enum EditableTrackKind
        {
            Animation,
            Notify,
            NotifyState,
            Custom
        }

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

        public void RefreshPlayModeReadonly()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            host.Unbind();
            host.Clear();
            boundObject = null;
            VisualElement editableRoot = CreateEditableRoot();
            if (EditorApplication.isPlaying)
                host.Add(CreatePlayModeReadonlyNotice());
            host.Add(editableRoot);

            if (TryBuildTimelineElementInspector(editableRoot))
            {
                ApplyPlayModeReadonly(editableRoot);
                return;
            }

            UnityEngine.Object selected = context.SelectedObject ?? context.Montage;
            if (selected is AnimMontageLibrarySO library && TryBuildLibraryInspector(editableRoot, library))
            {
                ApplyPlayModeReadonly(editableRoot);
                return;
            }

            if (selected is AnimMontageSO && TryBuildMontageInspector(editableRoot))
            {
                ApplyPlayModeReadonly(editableRoot);
                return;
            }

            if (selected == null)
            {
                editableRoot.Add(new Label("Select a montage or notify.")
                {
                    style = { whiteSpace = WhiteSpace.Normal }
                });
                return;
            }

            var editor = UnityEditor.Editor.CreateEditor(selected);
            var inspector = new InspectorElement(editor);
            inspector.style.flexGrow = 1;
            editableRoot.Add(inspector);
            ApplyPlayModeReadonly(editableRoot);
        }

        private bool TryBuildTimelineElementInspector(VisualElement root)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return false;

            if (TryBuildMultiSelectionInspector(root))
                return true;

            if (TryBuildTrackInspector(root))
                return true;

            boundObject = new SerializedObject(montage);
            if (context.SelectedSegmentIndex >= 0)
                return BuildArrayElementInspector(root,
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
                return BuildArrayElementInspector(root,
                    GetManagedReferenceTitle("Anim Notify", "notifies", context.SelectedNotifyIndex, "notify"),
                    "notifies",
                    context.SelectedNotifyIndex,
                    "notify",
                    "time",
                    "customColor");

            if (context.SelectedNotifyStateIndex >= 0)
                return BuildArrayElementInspector(root,
                    GetManagedReferenceTitle("Anim Notify State", "notifyStates", context.SelectedNotifyStateIndex, "notifyState"),
                    "notifyStates",
                    context.SelectedNotifyStateIndex,
                    "notifyState",
                    "startTime",
                    "endTime",
                    "customColor");

            if (context.SelectedCustomElementIndex >= 0)
                return BuildArrayElementInspector(root,
                    GetManagedReferenceTitle("Custom Element", "customElements", context.SelectedCustomElementIndex, "element"),
                    "customElements",
                    context.SelectedCustomElementIndex,
                    "element",
                    "startTime",
                    "endTime",
                    "customColor");

            return false;
        }

        private bool TryBuildMultiSelectionInspector(VisualElement root)
        {
            int segmentCount = context.SelectedSegmentIndices.Count;
            int notifyCount = context.SelectedNotifyIndices.Count;
            int stateCount = context.SelectedNotifyStateIndices.Count;
            int customCount = context.SelectedCustomElementIndices.Count;
            int trackCount = context.SelectedTimelineTrackKeys.Count;
            int totalCount = segmentCount + notifyCount + stateCount + customCount + trackCount;
            if (totalCount <= 1)
                return false;

            root.Add(new Label("Multi Selection")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            });

            root.Add(new Label($"{totalCount} timeline items selected.")
            {
                style =
                {
                    marginBottom = 8f,
                    whiteSpace = WhiteSpace.Normal,
                    color = new Color(0.78f, 0.82f, 0.9f, 0.9f)
                }
            });

            AddSelectionCountLabel(root, "Segments", segmentCount);
            AddSelectionCountLabel(root, "Notifies", notifyCount);
            AddSelectionCountLabel(root, "Notify States", stateCount);
            AddSelectionCountLabel(root, "Custom Elements", customCount);
            AddSelectionCountLabel(root, "Tracks", trackCount);
            return true;
        }

        private static void AddSelectionCountLabel(VisualElement root, string label, int count)
        {
            if (count <= 0)
                return;

            root.Add(new Label($"{label}: {count}")
            {
                style =
                {
                    marginBottom = 2f,
                    color = new Color(1f, 1f, 1f, 0.82f)
                }
            });
        }
        private bool TryBuildTrackInspector(VisualElement root)
        {
            if (context.SelectedTimelineTrackKeys.Count != 1)
                return false;

            string trackKey = null;
            foreach (string key in context.SelectedTimelineTrackKeys)
            {
                trackKey = key;
                break;
            }

            if (!TryParseTrackKey(trackKey, out EditableTrackKind kind, out string trackId))
                return false;

            bool isDefaultTrack = trackId == "Default";
            root.Add(new Label($"{GetTrackKindLabel(kind)} Track")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            });

            var card = new VisualElement
            {
                style =
                {
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 8,
                    paddingRight = 8,
                    marginBottom = 8,
                    backgroundColor = new Color(0.18f, 0.18f, 0.2f, 0.7f),
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(1f, 1f, 1f, 0.08f),
                    borderBottomColor = new Color(1f, 1f, 1f, 0.08f),
                    borderLeftColor = new Color(1f, 1f, 1f, 0.08f),
                    borderRightColor = new Color(1f, 1f, 1f, 0.08f),
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5
                }
            };

            card.Add(new Label($"Current: {trackId}")
            {
                style =
                {
                    marginBottom = 6,
                    color = new Color(0.78f, 0.82f, 0.9f, 0.9f),
                    whiteSpace = WhiteSpace.Normal
                }
            });

            var field = new TextField("Track Id")
            {
                value = trackId,
                isDelayed = true
            };
            field.SetEnabled(!isDefaultTrack);
            field.style.marginBottom = 6;
            card.Add(field);

            var applyButton = new Button(() => RenameTrack(kind, trackId, field.value))
            {
                text = "Apply Track Id"
            };
            applyButton.SetEnabled(!isDefaultTrack);
            applyButton.style.height = 24;
            applyButton.style.alignSelf = Align.FlexStart;
            applyButton.style.paddingLeft = 10;
            applyButton.style.paddingRight = 10;
            card.Add(applyButton);

            if (isDefaultTrack)
            {
                card.Add(new Label("Default track id cannot be renamed.")
                {
                    style =
                    {
                        marginTop = 6,
                        color = new Color(0.9f, 0.68f, 0.35f, 0.9f),
                        whiteSpace = WhiteSpace.Normal
                    }
                });
            }

            root.Add(card);
            return true;
        }

        private void RenameTrack(EditableTrackKind kind, string oldTrackId, string newTrackId)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || oldTrackId == "Default")
                return;

            newTrackId = string.IsNullOrWhiteSpace(newTrackId) ? "Default" : newTrackId.Trim();
            if (newTrackId == "Default" || newTrackId == oldTrackId)
                return;

            Undo.RecordObject(montage, "Rename Montage Track");
            SerializedObject so = new(montage);
            RenameTrackProperty(so, kind, oldTrackId, newTrackId);
            RenameTrackItems(so, kind, oldTrackId, newTrackId);
            RenameTrackOrderKey(so, kind, oldTrackId, newTrackId);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(montage);
            context.SetSelectedTimelineTrack(GetTrackKey(kind, newTrackId));
            context.NotifyExternalChange();
        }

        private static void RenameTrackProperty(SerializedObject so, EditableTrackKind kind, string oldTrackId, string newTrackId)
        {
            SerializedProperty property = so.FindProperty(GetTrackPropertyName(kind));
            if (property == null)
                return;

            if (kind == EditableTrackKind.Custom)
            {
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty track = property.GetArrayElementAtIndex(i);
                    SerializedProperty trackId = track.FindPropertyRelative("trackId");
                    if (trackId != null && trackId.stringValue == oldTrackId)
                        trackId.stringValue = newTrackId;
                }
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                if (item.stringValue == oldTrackId)
                    item.stringValue = newTrackId;
            }
        }

        private static void RenameTrackItems(SerializedObject so, EditableTrackKind kind, string oldTrackId, string newTrackId)
        {
            SerializedProperty items = so.FindProperty(GetElementPropertyName(kind));
            if (items == null)
                return;

            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                SerializedProperty trackId = item.FindPropertyRelative("trackId");
                if (trackId != null && trackId.stringValue == oldTrackId)
                    trackId.stringValue = newTrackId;
            }
        }

        private static void RenameTrackOrderKey(SerializedObject so, EditableTrackKind kind, string oldTrackId, string newTrackId)
        {
            SerializedProperty order = so.FindProperty("timelineTrackOrder");
            if (order == null)
                return;

            string oldKey = GetTrackKey(kind, oldTrackId);
            string newKey = GetTrackKey(kind, newTrackId);
            for (int i = 0; i < order.arraySize; i++)
            {
                SerializedProperty item = order.GetArrayElementAtIndex(i);
                if (item.stringValue == oldKey)
                    item.stringValue = newKey;
            }
        }

        private static bool TryParseTrackKey(string key, out EditableTrackKind kind, out string trackId)
        {
            kind = default;
            trackId = null;
            if (string.IsNullOrEmpty(key))
                return false;

            int split = key.IndexOf(':');
            if (split <= 0 || split >= key.Length - 1)
                return false;

            string kindText = key.Substring(0, split);
            trackId = key.Substring(split + 1);
            switch (kindText)
            {
                case "Segment":
                    kind = EditableTrackKind.Animation;
                    return true;
                case "Notify":
                    kind = EditableTrackKind.Notify;
                    return true;
                case "NotifyState":
                    kind = EditableTrackKind.NotifyState;
                    return true;
                case "Custom":
                    kind = EditableTrackKind.Custom;
                    return true;
                default:
                    return false;
            }
        }

        private static string GetTrackKey(EditableTrackKind kind, string trackId) =>
            $"{GetTrackKeyPrefix(kind)}:{trackId}";

        private static string GetTrackKeyPrefix(EditableTrackKind kind) =>
            kind switch
            {
                EditableTrackKind.Animation => "Segment",
                EditableTrackKind.Notify => "Notify",
                EditableTrackKind.NotifyState => "NotifyState",
                EditableTrackKind.Custom => "Custom",
                _ => string.Empty
            };

        private static string GetTrackKindLabel(EditableTrackKind kind) =>
            kind switch
            {
                EditableTrackKind.Animation => "Animation",
                EditableTrackKind.Notify => "Notify",
                EditableTrackKind.NotifyState => "Notify State",
                EditableTrackKind.Custom => "Custom",
                _ => "Timeline"
            };

        private static string GetTrackPropertyName(EditableTrackKind kind) =>
            kind switch
            {
                EditableTrackKind.Animation => "animationTracks",
                EditableTrackKind.Notify => "notifyTracks",
                EditableTrackKind.NotifyState => "notifyStateTracks",
                EditableTrackKind.Custom => "customTracks",
                _ => string.Empty
            };

        private static string GetElementPropertyName(EditableTrackKind kind) =>
            kind switch
            {
                EditableTrackKind.Animation => "segments",
                EditableTrackKind.Notify => "notifies",
                EditableTrackKind.NotifyState => "notifyStates",
                EditableTrackKind.Custom => "customElements",
                _ => string.Empty
            };

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

        private bool TryBuildLibraryInspector(VisualElement root, AnimMontageLibrarySO library)
        {
            if (library == null)
                return false;

            boundObject = new SerializedObject(library);
            root.Add(new Label("Montage Library")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            });

            SerializedProperty previewModel = boundObject.FindProperty("previewModel");
            if (previewModel != null)
            {
                var previewField = new PropertyField(previewModel, "Preview Model");
                previewField.RegisterValueChangeCallback(_ =>
                {
                    boundObject.ApplyModifiedProperties();
                    context.SetPreviewModel(library.PreviewModel);
                    context.NotifyExternalChange();
                });
                root.Add(previewField);
            }

            AddProperty(root, "montages");

            root.Add(new Label($"Montages: {library.Montages.Count}")
            {
                style =
                {
                    marginTop = 8f,
                    whiteSpace = WhiteSpace.Normal,
                    color = new Color(0.78f, 0.82f, 0.9f, 0.82f)
                }
            });

            root.Bind(boundObject);
            return true;
        }

        private bool TryBuildMontageInspector(VisualElement root)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return false;

            boundObject = new SerializedObject(montage);
            root.Add(new Label("Montage")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6f
                }
            });

            AddProperty(root, "rateScale");
            AddProperty(root, "applyRootMotion");

            root.Add(new Label(
                $"Segments: {montage.Segments.Count} | Notifies: {montage.Notifies.Count} | States: {montage.NotifyStates.Count}")
            {
                style =
                {
                    marginTop = 8f,
                    whiteSpace = WhiteSpace.Normal,
                    color = new Color(0.78f, 0.82f, 0.9f, 0.82f)
                }
            });

            root.Bind(boundObject);
            return true;
        }

        private void AddProperty(VisualElement root, string propertyName)
        {
            SerializedProperty property = boundObject.FindProperty(propertyName);
            if (property != null)
                root.Add(new PropertyField(property));
        }

        private bool BuildArrayElementInspector(VisualElement root, string title, string arrayPropertyName, int index, params string[] propertyNames)
        {
            SerializedProperty array = boundObject.FindProperty(arrayPropertyName);
            if (array == null || index < 0 || index >= array.arraySize)
                return false;

            root.Add(new Label(title)
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
                    root.Add(new PropertyField(property));
            }

            root.Bind(boundObject);
            return true;
        }

        private static VisualElement CreateEditableRoot() => new()
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                flexGrow = 1
            }
        };

        private static Label CreatePlayModeReadonlyNotice() => new("Play Mode: Montage asset editing is locked.")
        {
            style =
            {
                marginBottom = 8,
                paddingTop = 6,
                paddingBottom = 6,
                paddingLeft = 8,
                paddingRight = 8,
                whiteSpace = WhiteSpace.Normal,
                color = new Color(0.95f, 0.78f, 0.42f, 1f),
                backgroundColor = new Color(0.22f, 0.16f, 0.08f, 0.72f),
                borderTopLeftRadius = 5,
                borderTopRightRadius = 5,
                borderBottomLeftRadius = 5,
                borderBottomRightRadius = 5
            }
        };

        private static void ApplyPlayModeReadonly(VisualElement editableRoot)
        {
            if (!EditorApplication.isPlaying)
                return;

            editableRoot.SetEnabled(false);
            editableRoot.style.opacity = 0.55f;
        }
    }
}