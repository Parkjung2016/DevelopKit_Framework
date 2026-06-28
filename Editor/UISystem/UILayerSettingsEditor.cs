using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    [CustomEditor(typeof(UILayerSettings))]
    public sealed class UILayerSettingsEditor : Editor
    {
        private sealed class LayerListRowBinding
        {
            public int Index;
            public Label TitleLabel;
            public Label SubtitleLabel;
            public VisualElement BuiltInBadge;
            public VisualElement StackBadge;
            public Button OpenScreenButton;
        }

        private sealed class CanvasGroupListRowBinding
        {
            public int Index;
            public Label TitleLabel;
            public Label SubtitleLabel;
        }

        private VisualElement rootElement;
        private VisualElement canvasGroupsListHost;
        private VisualElement canvasGroupsDetailHost;
        private VisualElement layersListHost;
        private VisualElement layersDetailHost;
        private Label canvasGroupsCountLabel;
        private Label layersCountLabel;
        private ToolbarSearchField canvasGroupsSearchField;
        private ToolbarSearchField layersSearchField;
        private VisualElement subTabRow;
        private bool externalNotifyScheduled;
        private bool suppressExternalListRefresh;
        private ExternalNotifyFlags pendingExternalNotify = ExternalNotifyFlags.None;
        private IVisualElementScheduledItem layersSearchRebuildSchedule;
        private IVisualElementScheduledItem canvasGroupsSearchRebuildSchedule;
        private readonly Dictionary<int, LayerListRowBinding> layerRowBindings = new();
        private readonly Dictionary<int, CanvasGroupListRowBinding> canvasGroupRowBindings = new();
        private PopupField<string> openLayerCanvasGroupPopup;
        private string openLayerCanvasGroupPropPath;
        private PopupField<string> screenStackLayerPopup;
        private bool suppressScreenStackPopupCallback;

        private enum ExternalNotifyFlags
        {
            None = 0,
            Layers = 1 << 0,
            CanvasGroups = 1 << 1
        }

        private int selectedCanvasGroupIndex => canvasGroupSelection?.Primary ?? -1;
        private int selectedLayerIndex => layerSelection?.Primary ?? -1;
        private UISystemEditorListSelectionController canvasGroupSelection;
        private UISystemEditorListSelectionController layerSelection;
        private string canvasGroupsSearchText = string.Empty;
        private string layersSearchText = string.Empty;

        private UISystemEditorUI.LayerSettingsSection ActiveSection
        {
            get
            {
                if (UISystemEditorUI.PreferWindowLayout)
                    return UISystemEditorUI.LayerSettingsSectionMode;

                int index = EditorPrefs.GetInt(
                    UISystemEditorAssets.LayerSubTabPrefsKey,
                    (int)UISystemEditorUI.LayerSettingsSection.Layers);
                return index == 0
                    ? UISystemEditorUI.LayerSettingsSection.CanvasGroups
                    : UISystemEditorUI.LayerSettingsSection.Layers;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            rootElement = new VisualElement();
            rootElement.style.flexGrow = 1;
            var settings = (UILayerSettings)target;

            rootElement.Add(UISystemEditorUI.BuildHeader(
                GetHeaderTitle(),
                GetHeaderSubtitle()));

            if (!UISystemEditorUI.PreferWindowLayout)
            {
                subTabRow = UISystemEditorUI.BuildSubTabRow(
                    new[] { "Canvas 묶음", "레이어" },
                    ActiveSection == UISystemEditorUI.LayerSettingsSection.CanvasGroups ? 0 : 1,
                    OnStandaloneSubTabSelected);
                rootElement.Add(subTabRow);
                rootElement.Add(UISystemEditorUI.BuildOpenSettingsToolbar(serializedObject.targetObject));
            }

            if (UISystemEditorUI.PreferWindowLayout)
            {
                if (ActiveSection == UISystemEditorUI.LayerSettingsSection.CanvasGroups)
                    rootElement.Add(BuildCanvasGroupsPanel(settings));
                else
                    rootElement.Add(BuildLayersPanel(settings));
            }
            else
            {
                var canvasPanel = BuildCanvasGroupsPanel(settings);
                var layersPanel = BuildLayersPanel(settings);
                canvasPanel.name = "canvas-groups-panel";
                layersPanel.name = "layers-panel";
                rootElement.Add(canvasPanel);
                rootElement.Add(layersPanel);
                ApplySectionVisibility();
            }

            rootElement.RegisterCallback<AttachToPanelEvent>(_ =>
                UISystemEditorCanvasGroups.CanvasGroupsChanged += OnExternalCanvasGroupsChanged);
            rootElement.RegisterCallback<DetachFromPanelEvent>(_ =>
                UISystemEditorCanvasGroups.CanvasGroupsChanged -= OnExternalCanvasGroupsChanged);

            return rootElement;
        }

        private static string GetHeaderTitle() =>
            UISystemEditorUI.PreferWindowLayout
                ? UISystemEditorUI.LayerSettingsSectionMode == UISystemEditorUI.LayerSettingsSection.CanvasGroups
                    ? "Canvas 묶음"
                    : "프로젝트 레이어"
                : "UI 레이어 설정";

        private static string GetHeaderSubtitle() =>
            UISystemEditorUI.PreferWindowLayout
                ? UISystemEditorUI.LayerSettingsSectionMode == UISystemEditorUI.LayerSettingsSection.CanvasGroups
                    ? "물리 Canvas와 sortingOrder를 정의합니다."
                    : "레이어 ID와 Canvas 묶음 연결을 정의합니다."
                : "Canvas 묶음과 레이어를 나눠 편집합니다.";

        private void OnStandaloneSubTabSelected(int index)
        {
            EditorPrefs.SetInt(UISystemEditorAssets.LayerSubTabPrefsKey, index);
            ApplySectionVisibility();
            UpdateSubTabStyles();
        }

        private void UpdateSubTabStyles()
        {
            if (subTabRow == null)
                return;

            int selected = ActiveSection == UISystemEditorUI.LayerSettingsSection.CanvasGroups ? 0 : 1;
            for (int i = 0; i < subTabRow.childCount; i++)
            {
                if (subTabRow[i] is Button button)
                    UISystemEditorUI.ApplySubTabStyle(button, i == selected);
            }
        }

        private void ApplySectionVisibility()
        {
            bool showCanvas = ActiveSection == UISystemEditorUI.LayerSettingsSection.CanvasGroups;
            var canvasPanel = rootElement.Q("canvas-groups-panel");
            var layersPanel = rootElement.Q("layers-panel");
            if (canvasPanel != null)
                canvasPanel.style.display = showCanvas ? DisplayStyle.Flex : DisplayStyle.None;
            if (layersPanel != null)
                layersPanel.style.display = showCanvas ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement BuildCanvasGroupsPanel(UILayerSettings settings)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;

            var setupFoldout = new Foldout { text = "추가 · 초기화", value = false };
            setupFoldout.style.marginBottom = 6;
            setupFoldout.Add(UISystemEditorUI.BuildToolbar(
                ("기본값으로 초기화", () => ResetAll(settings)),
                ("빈 묶음 추가", AddEmptyCanvasGroup),
                ("기본 묶음 추가…", ShowAddBuiltInCanvasGroupMenu)));
            panel.Add(setupFoldout);

            panel.Add(BuildCanvasGroupsFilterBar());

            canvasGroupsCountLabel = UISystemEditorUI.BuildSectionLabel("Canvas 묶음");
            panel.Add(canvasGroupsCountLabel);

            var body = BuildMasterDetailBody(
                out canvasGroupsListHost,
                out canvasGroupsDetailHost);
            canvasGroupSelection = new UISystemEditorListSelectionController(
                canvasGroupsListHost,
                RebuildCanvasGroupsDetail,
                DeleteSelectedCanvasGroups);
            panel.Add(body);

            RebuildCanvasGroupsAll();
            return panel;
        }

        private VisualElement BuildLayersPanel(UILayerSettings settings)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;

            var setupFoldout = new Foldout { text = "추가 · 초기화 · 참고", value = false };
            setupFoldout.style.marginBottom = 6;
            setupFoldout.Add(UISystemEditorUI.BuildToolbar(
                ("기본값으로 초기화", () => ResetAll(settings)),
                ("빈 레이어 추가", AddEmptyLayer),
                ("기본 레이어 추가…", ShowAddBuiltInMenu)));
            setupFoldout.Add(UISystemEditorUI.BuildBuiltInLayersReference(asFoldout: false));
            panel.Add(setupFoldout);

            panel.Add(BuildLayersFilterBar());
            panel.Add(BuildScreenStackLayerSelector());

            layersCountLabel = UISystemEditorUI.BuildSectionLabel("레이어");
            panel.Add(layersCountLabel);

            var body = BuildMasterDetailBody(
                out layersListHost,
                out layersDetailHost);
            layerSelection = new UISystemEditorListSelectionController(
                layersListHost,
                RebuildLayersDetail,
                DeleteSelectedLayers);
            panel.Add(body);

            RebuildLayersAll();
            return panel;
        }

        private static VisualElement BuildMasterDetailBody(out VisualElement listHost, out VisualElement detailHost)
        {
            return UISystemEditorUI.BuildMasterDetailSplit(out listHost, out detailHost);
        }

        private VisualElement BuildCanvasGroupsFilterBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;

            canvasGroupsSearchField = new ToolbarSearchField();
            canvasGroupsSearchField.style.flexGrow = 1;
            canvasGroupsSearchField.style.flexShrink = 1;
            canvasGroupsSearchField.style.minWidth = 100;
            canvasGroupsSearchField.style.marginRight = 6;
            canvasGroupsSearchField.RegisterValueChangedCallback(evt =>
            {
                canvasGroupsSearchText = evt.newValue ?? string.Empty;
                ScheduleCanvasGroupsListRebuild();
            });
            bar.Add(canvasGroupsSearchField);

            var clear = new Button(() =>
            {
                canvasGroupSelection?.ClearSelection();
                RebuildCanvasGroupsDetail();
            })
            { text = "선택 해제" };
            clear.style.height = 22;
            clear.style.flexShrink = 0;
            bar.Add(clear);

            return bar;
        }

        private VisualElement BuildLayersFilterBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;

            layersSearchField = new ToolbarSearchField();
            layersSearchField.style.flexGrow = 1;
            layersSearchField.style.flexShrink = 1;
            layersSearchField.style.minWidth = 100;
            layersSearchField.style.marginRight = 6;
            layersSearchField.RegisterValueChangedCallback(evt =>
            {
                layersSearchText = evt.newValue ?? string.Empty;
                ScheduleLayersListRebuild();
            });
            bar.Add(layersSearchField);

            var clear = new Button(() =>
            {
                layerSelection?.ClearSelection();
                RebuildLayersDetail();
            })
            { text = "선택 해제" };
            clear.style.height = 22;
            clear.style.flexShrink = 0;
            bar.Add(clear);

            return bar;
        }

        private VisualElement BuildScreenStackLayerSelector()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var label = new Label("OpenScreen");
            label.style.minWidth = 72;
            label.style.marginRight = 4;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(label);

            screenStackLayerPopup = new PopupField<string>(new List<string>(), 0);
            screenStackLayerPopup.style.minWidth = 140;
            screenStackLayerPopup.style.flexShrink = 0;
            screenStackLayerPopup.style.marginRight = 8;
            screenStackLayerPopup.RegisterValueChangedCallback(evt =>
            {
                if (suppressScreenStackPopupCallback)
                    return;

                ApplyScreenStackLayer(evt.newValue);
            });
            row.Add(screenStackLayerPopup);

            var hint = UISystemEditorUI.BuildHint(
                "OpenScreen() 전환 레이어 · Ctrl+A 전체 선택 · Shift/Ctrl 클릭·드래그 다중 선택 · Delete 삭제");
            hint.style.marginBottom = 0;
            hint.style.flexGrow = 1;
            row.Add(hint);

            return row;
        }

        private void ResetAll(UILayerSettings settings)
        {
            Undo.RecordObject(settings, "UI 레이어 초기화");
            settings.ResetToBuiltInDefaults();
            EditorUtility.SetDirty(settings);
            RebuildActiveLists();
            QueueExternalNotify();
        }

        private void RebuildActiveLists()
        {
            if (canvasGroupsListHost != null)
                RebuildCanvasGroupsAll();
            if (layersListHost != null)
                RebuildLayersAll();
        }

        private void RebuildCanvasGroupsAll()
        {
            RebuildCanvasGroupsList();
            RebuildCanvasGroupsDetail();
        }

        private void RebuildLayersAll()
        {
            RebuildLayersList();
            RebuildLayersDetail();
        }

        private void RebuildCanvasGroupsList()
        {
            canvasGroupSelection?.ClearListRows();
            canvasGroupRowBindings.Clear();
            serializedObject.Update();

            SerializedProperty groups = serializedObject.FindProperty("canvasGroups");
            int total = groups.arraySize;
            int visible = CountVisibleCanvasGroups(groups);
            canvasGroupsCountLabel.text = visible == total
                ? $"Canvas 묶음 ({total})"
                : $"Canvas 묶음 ({visible} / {total})";

            if (total == 0)
            {
                canvasGroupsListHost.Add(UISystemEditorUI.BuildHelpBox(
                    "Canvas 묶음이 없습니다. '추가 · 초기화'에서 항목을 추가하세요.",
                    HelpBoxMessageType.Warning));
                canvasGroupSelection?.ClearSelection();
                return;
            }

            canvasGroupSelection?.PruneInvalidIndices(total);

            bool anyVisible = false;
            for (int i = 0; i < total; i++)
            {
                SerializedProperty element = groups.GetArrayElementAtIndex(i);
                if (!MatchesCanvasGroupFilter(element))
                    continue;

                anyVisible = true;
                canvasGroupsListHost.Add(BuildCanvasGroupListRow(element, i, groups));
            }

            if (!anyVisible)
            {
                canvasGroupsListHost.Add(UISystemEditorUI.BuildHelpBox(
                    "검색 조건에 맞는 Canvas 묶음이 없습니다.",
                    HelpBoxMessageType.Info));
            }

            canvasGroupSelection?.RefreshAllRowStyles();
        }

        private void RebuildCanvasGroupsDetail()
        {
            canvasGroupsDetailHost.Clear();

            SerializedProperty groups = serializedObject.FindProperty("canvasGroups");
            if (canvasGroupSelection != null && canvasGroupSelection.Count > 1)
            {
                canvasGroupsDetailHost.Add(UISystemEditorUI.BuildHelpBox(
                    $"{canvasGroupSelection.Count}개 선택됨 · Ctrl+A 전체 선택 · Delete 키로 삭제 · Shift/Ctrl+클릭·드래그로 다중 선택",
                    HelpBoxMessageType.Info));
                return;
            }

            if (selectedCanvasGroupIndex < 0 || selectedCanvasGroupIndex >= groups.arraySize)
            {
                canvasGroupsDetailHost.Add(UISystemEditorUI.BuildHelpBox(
                    "목록에서 Canvas 묶음을 선택하면 상세 설정을 편집할 수 있습니다.",
                    HelpBoxMessageType.Info));
                return;
            }

            canvasGroupsDetailHost.Add(BuildCanvasGroupDetailPanel(
                groups.GetArrayElementAtIndex(selectedCanvasGroupIndex),
                selectedCanvasGroupIndex,
                groups));
        }

        private void RebuildLayersList()
        {
            layerSelection?.ClearListRows();
            layerRowBindings.Clear();
            serializedObject.Update();

            SerializedProperty layers = serializedObject.FindProperty("layers");
            int total = layers.arraySize;
            int visible = CountVisibleLayers(layers);
            layersCountLabel.text = visible == total
                ? $"레이어 ({total})"
                : $"레이어 ({visible} / {total})";

            if (total == 0)
            {
                layersListHost.Add(UISystemEditorUI.BuildHelpBox(
                    "레이어가 없습니다. '추가 · 초기화 · 참고'에서 항목을 추가하세요.",
                    HelpBoxMessageType.Warning));
                layerSelection?.ClearSelection();
                return;
            }

            layerSelection?.PruneInvalidIndices(total);
            EnsureScreenStackLayerAssigned();
            SyncScreenStackLayerPopup();

            bool anyVisible = false;
            for (int i = 0; i < total; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (!MatchesLayerFilter(element))
                    continue;

                anyVisible = true;
                layersListHost.Add(BuildLayerListRow(element, i, layers));
            }

            if (!anyVisible)
            {
                layersListHost.Add(UISystemEditorUI.BuildHelpBox(
                    "검색 조건에 맞는 레이어가 없습니다.",
                    HelpBoxMessageType.Info));
            }

            layerSelection?.RefreshAllRowStyles();
        }

        private void RebuildLayersDetail()
        {
            layersDetailHost.Clear();
            openLayerCanvasGroupPopup = null;
            openLayerCanvasGroupPropPath = null;

            SerializedProperty layers = serializedObject.FindProperty("layers");
            if (layerSelection != null && layerSelection.Count > 1)
            {
                layersDetailHost.Add(UISystemEditorUI.BuildHelpBox(
                    $"{layerSelection.Count}개 선택됨 · Ctrl+A 전체 선택 · Delete 키로 삭제 · Shift/Ctrl+클릭·드래그로 다중 선택",
                    HelpBoxMessageType.Info));
                return;
            }

            if (selectedLayerIndex < 0 || selectedLayerIndex >= layers.arraySize)
            {
                layersDetailHost.Add(UISystemEditorUI.BuildHelpBox(
                    "목록에서 레이어를 선택하면 상세 설정을 편집할 수 있습니다.",
                    HelpBoxMessageType.Info));
                return;
            }

            layersDetailHost.Add(BuildLayerDetailPanel(
                layers.GetArrayElementAtIndex(selectedLayerIndex),
                selectedLayerIndex,
                layers));
        }

        private void OnExternalCanvasGroupsChanged()
        {
            if (suppressExternalListRefresh || layersListHost == null)
                return;

            RefreshAllLayerListRowSubtitles();
            SyncOpenLayerDetailCanvasGroupPopup();
        }

        private void ScheduleLayersListRebuild()
        {
            layersSearchRebuildSchedule?.Pause();
            layersSearchRebuildSchedule = layersSearchField?.schedule.Execute(RebuildLayersList).StartingIn(150);
        }

        private void ScheduleCanvasGroupsListRebuild()
        {
            canvasGroupsSearchRebuildSchedule?.Pause();
            canvasGroupsSearchRebuildSchedule = canvasGroupsSearchField?.schedule
                .Execute(RebuildCanvasGroupsList)
                .StartingIn(150);
        }

        private void QueueExternalNotify(ExternalNotifyFlags flags = ExternalNotifyFlags.Layers | ExternalNotifyFlags.CanvasGroups)
        {
            pendingExternalNotify |= flags;
            if (externalNotifyScheduled || rootElement?.panel == null)
                return;

            externalNotifyScheduled = true;
            rootElement.schedule.Execute(() =>
            {
                externalNotifyScheduled = false;
                ExternalNotifyFlags notify = pendingExternalNotify;
                pendingExternalNotify = ExternalNotifyFlags.None;

                suppressExternalListRefresh = true;
                try
                {
                    if ((notify & ExternalNotifyFlags.Layers) != 0)
                        UISystemEditorLayers.NotifyLayerIdChanged();

                    if ((notify & ExternalNotifyFlags.CanvasGroups) != 0)
                        UISystemEditorCanvasGroups.NotifyCanvasGroupsChanged();
                }
                finally
                {
                    suppressExternalListRefresh = false;
                }
            }).StartingIn(120);
        }

        private void AddEmptyCanvasGroup()
        {
            SerializedProperty groups = serializedObject.FindProperty("canvasGroups");
            groups.InsertArrayElementAtIndex(groups.arraySize);
            SerializedProperty element = groups.GetArrayElementAtIndex(groups.arraySize - 1);
            element.FindPropertyRelative("groupId").stringValue = "CustomCanvas";
            element.FindPropertyRelative("displayName").stringValue = "커스텀 Canvas";
            element.FindPropertyRelative("sortingOrder").intValue = 150;
            element.FindPropertyRelative("canvasName").stringValue = "Custom Canvas";
            serializedObject.ApplyModifiedProperties();
            canvasGroupSelection?.SelectSingle(groups.arraySize - 1);
            RebuildCanvasGroupsAll();
            QueueExternalNotify();
        }

        private void AddEmptyLayer()
        {
            SerializedProperty layers = serializedObject.FindProperty("layers");
            layers.InsertArrayElementAtIndex(layers.arraySize);
            SerializedProperty element = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            element.FindPropertyRelative("layerId").stringValue = "CustomLayer";
            element.FindPropertyRelative("displayName").stringValue = "커스텀 레이어";
            element.FindPropertyRelative("sortOrder").intValue = 250;
            element.FindPropertyRelative("canvasGroupId").stringValue = UICanvasGroups.Floating;
            element.FindPropertyRelative("useScreenStack").boolValue = false;
            element.FindPropertyRelative("rootName").stringValue = "CustomLayer";
            serializedObject.ApplyModifiedProperties();
            layerSelection?.SelectSingle(layers.arraySize - 1);
            RebuildLayersAll();
            QueueExternalNotify();
        }

        private void ShowAddBuiltInCanvasGroupMenu()
        {
            var menu = new GenericMenu();
            IReadOnlyList<BuiltInCanvasGroupInfo> builtIn = UISystemBuiltIn.CanvasGroups;
            for (int i = 0; i < builtIn.Count; i++)
            {
                BuiltInCanvasGroupInfo info = builtIn[i];
                menu.AddItem(new GUIContent(info.GroupId), false, () => AddBuiltInCanvasGroup(info));
            }

            menu.ShowAsContext();
        }

        private void AddBuiltInCanvasGroup(BuiltInCanvasGroupInfo info)
        {
            SerializedProperty groups = serializedObject.FindProperty("canvasGroups");
            for (int i = 0; i < groups.arraySize; i++)
            {
                if (groups.GetArrayElementAtIndex(i).FindPropertyRelative("groupId").stringValue == info.GroupId)
                {
                    EditorUtility.DisplayDialog("UI 레이어 설정", $"Canvas 묶음 '{info.GroupId}'가 이미 있습니다.", "확인");
                    return;
                }
            }

            groups.InsertArrayElementAtIndex(groups.arraySize);
            ApplyBuiltInCanvasGroup(groups.GetArrayElementAtIndex(groups.arraySize - 1), info);
            serializedObject.ApplyModifiedProperties();
            canvasGroupSelection?.SelectSingle(groups.arraySize - 1);
            RebuildCanvasGroupsAll();
            QueueExternalNotify();
        }

        private static void ApplyBuiltInCanvasGroup(SerializedProperty element, BuiltInCanvasGroupInfo info)
        {
            element.FindPropertyRelative("groupId").stringValue = info.GroupId;
            element.FindPropertyRelative("displayName").stringValue = info.GroupId;
            element.FindPropertyRelative("sortingOrder").intValue = info.SortingOrder;
            element.FindPropertyRelative("canvasName").stringValue = info.CanvasName;
        }

        private void ShowAddBuiltInMenu()
        {
            var menu = new GenericMenu();
            IReadOnlyList<BuiltInLayerInfo> builtIn = UISystemBuiltIn.Layers;
            for (int i = 0; i < builtIn.Count; i++)
            {
                BuiltInLayerInfo info = builtIn[i];
                menu.AddItem(new GUIContent(info.LayerId), false, () => AddBuiltInLayer(info));
            }

            menu.ShowAsContext();
        }

        private void AddBuiltInLayer(BuiltInLayerInfo info)
        {
            SerializedProperty layers = serializedObject.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("layerId").stringValue == info.LayerId)
                {
                    EditorUtility.DisplayDialog("UI 레이어 설정", $"레이어 '{info.LayerId}'가 이미 있습니다.", "확인");
                    return;
                }
            }

            layers.InsertArrayElementAtIndex(layers.arraySize);
            ApplyBuiltInToProperty(layers.GetArrayElementAtIndex(layers.arraySize - 1), info);
            serializedObject.ApplyModifiedProperties();
            layerSelection?.SelectSingle(layers.arraySize - 1);
            RebuildLayersAll();
            QueueExternalNotify();
        }

        private static void ApplyBuiltInToProperty(SerializedProperty element, BuiltInLayerInfo info)
        {
            element.FindPropertyRelative("layerId").stringValue = info.LayerId;
            element.FindPropertyRelative("displayName").stringValue = info.LayerId;
            element.FindPropertyRelative("sortOrder").intValue = info.SortOrder;
            element.FindPropertyRelative("canvasGroupId").stringValue = info.CanvasGroupId;
            element.FindPropertyRelative("useScreenStack").boolValue = info.UseScreenStack;
            element.FindPropertyRelative("rootName").stringValue = info.RootName;
        }

        private VisualElement BuildCanvasGroupListRow(SerializedProperty element, int index, SerializedProperty groupsArray)
        {
            SerializedProperty groupIdProp = element.FindPropertyRelative("groupId");
            string groupId = groupIdProp.stringValue;

            var row = CreateSelectableListRow(index, canvasGroupSelection);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;
            textCol.style.minWidth = 0;

            var title = new Label(groupId);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(title);

            var sub = new Label();
            sub.style.fontSize = 9;
            sub.style.color = new Color(0.65f, 0.65f, 0.65f);
            sub.style.overflow = Overflow.Hidden;
            sub.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(sub);
            row.Add(textCol);

            if (UISystemBuiltIn.IsBuiltInCanvasGroupId(groupId))
                row.Add(UISystemEditorUI.BuildBadge("기본", new Color(0.6f, 0.85f, 1f)));

            var binding = new CanvasGroupListRowBinding
            {
                Index = index,
                TitleLabel = title,
                SubtitleLabel = sub
            };
            canvasGroupRowBindings[index] = binding;
            RefreshCanvasGroupListRow(binding);

            return row;
        }

        private VisualElement BuildLayerListRow(SerializedProperty element, int index, SerializedProperty layersArray)
        {
            SerializedProperty layerIdProp = element.FindPropertyRelative("layerId");
            string layerId = layerIdProp.stringValue;

            var row = CreateSelectableListRow(index, layerSelection);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;
            textCol.style.minWidth = 0;

            var title = new Label(layerId);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(title);

            var sub = new Label();
            sub.style.fontSize = 9;
            sub.style.color = new Color(0.65f, 0.65f, 0.65f);
            sub.style.overflow = Overflow.Hidden;
            sub.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(sub);
            row.Add(textCol);

            var badges = new VisualElement();
            badges.style.flexDirection = FlexDirection.Row;
            badges.style.flexShrink = 0;

            var builtInBadge = UISystemEditorUI.BuildBadge("기본", new Color(0.6f, 0.85f, 1f));
            builtInBadge.style.display = DisplayStyle.None;
            badges.Add(builtInBadge);

            var stackBadge = UISystemEditorUI.BuildBadge("OpenScreen", new Color(0.75f, 0.85f, 1f));
            stackBadge.style.display = DisplayStyle.None;
            badges.Add(stackBadge);

            bool isOpenScreenLayer = IsScreenStackLayerId(layerId);
            var openScreenButton = new Button(() => ApplyScreenStackLayer(layerId))
            { text = "OpenScreen" };
            openScreenButton.style.height = 18;
            openScreenButton.style.fontSize = 9;
            openScreenButton.style.marginLeft = 2;
            openScreenButton.style.flexShrink = 0;
            ApplyOpenScreenButtonStyle(openScreenButton, isOpenScreenLayer);
            openScreenButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            openScreenButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            row.Add(openScreenButton);

            row.Add(badges);

            var binding = new LayerListRowBinding
            {
                Index = index,
                TitleLabel = title,
                SubtitleLabel = sub,
                BuiltInBadge = builtInBadge,
                StackBadge = stackBadge,
                OpenScreenButton = openScreenButton
            };
            layerRowBindings[index] = binding;
            RefreshLayerListRow(binding);

            return row;
        }

        private static VisualElement CreateSelectableListRow(int index, UISystemEditorListSelectionController selection)
        {
            var row = new VisualElement();
            row.userData = index;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 4;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.marginBottom = 1;
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;
            UISystemEditorListSelectionStyles.PrepareRow(row, selection != null && selection.IsSelected(index));

            return row;
        }

        private void DeleteSelectedCanvasGroups()
        {
            if (canvasGroupSelection == null || canvasGroupSelection.Count == 0)
                return;

            SerializedProperty groups = serializedObject.FindProperty("canvasGroups");
            UISystemEditorListSelectionDelete.DeleteDescending(
                groups,
                canvasGroupSelection.GetSelectedSnapshot(),
                target,
                "Delete Canvas Groups");
            canvasGroupSelection.ClearSelection();
            RebuildCanvasGroupsAll();
            QueueExternalNotify(ExternalNotifyFlags.CanvasGroups | ExternalNotifyFlags.Layers);
        }

        private void DeleteSelectedLayers()
        {
            if (layerSelection == null || layerSelection.Count == 0)
                return;

            SerializedProperty layers = serializedObject.FindProperty("layers");
            UISystemEditorListSelectionDelete.DeleteDescending(
                layers,
                layerSelection.GetSelectedSnapshot(),
                target,
                "Delete UI Layers");
            layerSelection.ClearSelection();
            EnsureScreenStackLayerAssigned();
            RebuildLayersAll();
            QueueExternalNotify(ExternalNotifyFlags.Layers);
        }

        private static void ApplyOpenScreenButtonStyle(Button button, bool active)
        {
            if (active)
            {
                button.style.backgroundColor = new Color(0.2f, 0.45f, 0.7f, 0.45f);
                button.style.color = new Color(0.9f, 0.95f, 1f);
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                button.style.backgroundColor = new Color(0f, 0f, 0f, 0.1f);
                button.style.color = new Color(0.75f, 0.75f, 0.75f);
                button.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        private VisualElement BuildCanvasGroupDetailPanel(
            SerializedProperty element,
            int index,
            SerializedProperty groupsArray)
        {
            SerializedProperty groupIdProp = element.FindPropertyRelative("groupId");
            SerializedProperty sortingOrderProp = element.FindPropertyRelative("sortingOrder");
            string groupId = groupIdProp.stringValue;

            var panel = UISystemEditorUI.BuildCard();
            panel.style.marginBottom = 0;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label(groupId);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.flexGrow = 1;
            header.Add(title);

            var builtInBadgeHost = new VisualElement();
            builtInBadgeHost.style.flexDirection = FlexDirection.Row;
            header.Add(builtInBadgeHost);

            var remove = new Button(() =>
            {
                groupsArray.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                if (selectedCanvasGroupIndex >= groupsArray.arraySize)
                    canvasGroupSelection?.ClearSelection();
                RebuildCanvasGroupsAll();
                QueueExternalNotify();
            })
            { text = "삭제" };
            remove.style.height = 20;
            header.Add(remove);
            panel.Add(header);

            void RefreshCanvasGroupDetailPresentation(string currentGroupId)
            {
                title.text = currentGroupId;

                builtInBadgeHost.Clear();
                if (UISystemBuiltIn.IsBuiltInCanvasGroupId(currentGroupId))
                {
                    builtInBadgeHost.Add(UISystemEditorUI.BuildBadge(
                        "기본",
                        new Color(0.6f, 0.85f, 1f)));
                }
            }

            RefreshCanvasGroupDetailPresentation(groupId);

            string canvasBuiltInDescription = null;
            if (UISystemBuiltIn.TryGetCanvasGroup(groupId, out BuiltInCanvasGroupInfo canvasBuiltInInfo))
                canvasBuiltInDescription = canvasBuiltInInfo.Description;

            panel.Add(UISystemEditorUI.BuildEditableHint(
                serializedObject,
                target,
                element.FindPropertyRelative("description"),
                canvasBuiltInDescription));

            var fields = UISystemEditorUI.BuildFieldGroup("속성");
            BindRenamableStringField(
                fields,
                groupIdProp,
                "묶음 ID",
                groupId,
                (oldGroupId, newGroupId) =>
                {
                    UISystemEditorReferencePropagation.PropagateCanvasGroupIdRename(
                        (UILayerSettings)target,
                        oldGroupId,
                        newGroupId);
                },
                () =>
                {
                    RefreshCanvasGroupDetailPresentation(groupIdProp.stringValue);
                    RefreshCanvasGroupListRow(selectedCanvasGroupIndex);
                    RefreshAllLayerListRowSubtitles();
                    SyncOpenLayerDetailCanvasGroupPopup();
                },
                ExternalNotifyFlags.CanvasGroups | ExternalNotifyFlags.Layers);
            BindField(fields, element.FindPropertyRelative("displayName"), "표시 이름", () =>
                QueueExternalNotify(ExternalNotifyFlags.CanvasGroups));
            BindField(fields, sortingOrderProp, "sortingOrder", () =>
            {
                RefreshCanvasGroupListRow(selectedCanvasGroupIndex);
                QueueExternalNotify(ExternalNotifyFlags.CanvasGroups);
            });
            BindField(fields, element.FindPropertyRelative("canvasName"), "Canvas 이름", () =>
            {
                RefreshCanvasGroupListRow(selectedCanvasGroupIndex);
                QueueExternalNotify(ExternalNotifyFlags.CanvasGroups);
            });
            panel.Add(fields);

            return panel;
        }

        private VisualElement BuildLayerDetailPanel(
            SerializedProperty element,
            int index,
            SerializedProperty layersArray)
        {
            SerializedProperty layerIdProp = element.FindPropertyRelative("layerId");
            string layerId = layerIdProp.stringValue;

            var panel = UISystemEditorUI.BuildCard();
            panel.style.marginBottom = 0;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label(layerId);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.flexGrow = 1;
            header.Add(title);

            var builtInBadgeHost = new VisualElement();
            builtInBadgeHost.style.flexDirection = FlexDirection.Row;
            header.Add(builtInBadgeHost);

            var remove = new Button(() =>
            {
                layersArray.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                if (selectedLayerIndex >= layersArray.arraySize)
                    layerSelection?.ClearSelection();
                EnsureScreenStackLayerAssigned();
                RebuildLayersAll();
                QueueExternalNotify();
            })
            { text = "삭제" };
            remove.style.height = 20;
            header.Add(remove);
            panel.Add(header);

            var screenStackInfo = UISystemEditorUI.BuildHint(
                "이 레이어가 OpenScreen() 화면 전환 스택입니다. Popup/OpenPopup과는 무관합니다.");
            screenStackInfo.style.display = DisplayStyle.None;
            panel.Add(screenStackInfo);

            void RefreshLayerDetailPresentation(string currentLayerId)
            {
                title.text = currentLayerId;

                builtInBadgeHost.Clear();
                if (UISystemBuiltIn.IsBuiltInLayerId(currentLayerId))
                {
                    builtInBadgeHost.Add(UISystemEditorUI.BuildBadge(
                        "기본",
                        new Color(0.6f, 0.85f, 1f)));
                }

                screenStackInfo.style.display = IsScreenStackLayerId(currentLayerId)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            RefreshLayerDetailPresentation(layerId);

            string layerBuiltInDescription = null;
            if (UISystemBuiltIn.TryGetLayer(layerId, out BuiltInLayerInfo layerBuiltInInfo))
                layerBuiltInDescription = layerBuiltInInfo.Description;

            panel.Add(UISystemEditorUI.BuildEditableHint(
                serializedObject,
                target,
                element.FindPropertyRelative("description"),
                layerBuiltInDescription));

            var fields = UISystemEditorUI.BuildFieldGroup("속성");
            BindRenamableStringField(
                fields,
                layerIdProp,
                "레이어 ID",
                layerId,
                (oldLayerId, newLayerId) =>
                {
                    UISystemEditorReferencePropagation.PropagateLayerIdRename(
                        (UILayerSettings)target,
                        oldLayerId,
                        newLayerId);
                },
                () =>
                {
                    RefreshLayerDetailPresentation(layerIdProp.stringValue);
                    RefreshAllLayerListRows();
                    SyncScreenStackLayerPopup();
                },
                ExternalNotifyFlags.Layers);
            BindField(fields, element.FindPropertyRelative("displayName"), "표시 이름", () =>
                QueueExternalNotify(ExternalNotifyFlags.Layers));
            BindField(fields, element.FindPropertyRelative("sortOrder"), "정렬 순서", () =>
            {
                RefreshLayerListRow(selectedLayerIndex);
                QueueExternalNotify(ExternalNotifyFlags.Layers);
            });
            BindCanvasGroupField(fields, element.FindPropertyRelative("canvasGroupId"), () =>
            {
                RefreshLayerListRow(selectedLayerIndex);
                QueueExternalNotify(ExternalNotifyFlags.Layers | ExternalNotifyFlags.CanvasGroups);
            });
            BindField(fields, element.FindPropertyRelative("rootName"), "루트 이름", () =>
                QueueExternalNotify(ExternalNotifyFlags.Layers));
            panel.Add(fields);

            return panel;
        }

        private int CountVisibleCanvasGroups(SerializedProperty groups)
        {
            int count = 0;
            for (int i = 0; i < groups.arraySize; i++)
            {
                if (MatchesCanvasGroupFilter(groups.GetArrayElementAtIndex(i)))
                    count++;
            }

            return count;
        }

        private int CountVisibleLayers(SerializedProperty layers)
        {
            int count = 0;
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (MatchesLayerFilter(layers.GetArrayElementAtIndex(i)))
                    count++;
            }

            return count;
        }

        private bool MatchesCanvasGroupFilter(SerializedProperty element)
        {
            if (string.IsNullOrEmpty(canvasGroupsSearchText))
                return true;

            string query = canvasGroupsSearchText.Trim();
            string groupId = element.FindPropertyRelative("groupId").stringValue;
            if (groupId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string displayName = element.FindPropertyRelative("displayName").stringValue;
            if (displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string canvasName = element.FindPropertyRelative("canvasName").stringValue;
            return canvasName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesLayerFilter(SerializedProperty element)
        {
            if (string.IsNullOrEmpty(layersSearchText))
                return true;

            string query = layersSearchText.Trim();
            string layerId = element.FindPropertyRelative("layerId").stringValue;
            if (layerId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string displayName = element.FindPropertyRelative("displayName").stringValue;
            if (displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string groupId = element.FindPropertyRelative("canvasGroupId").stringValue;
            if (groupId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string rootName = element.FindPropertyRelative("rootName").stringValue;
            return rootName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BindRenamableStringField(
            VisualElement root,
            SerializedProperty property,
            string label,
            string initialValue,
            Action<string, string> onRenamed,
            Action onChanged,
            ExternalNotifyFlags notifyFlags = ExternalNotifyFlags.Layers)
        {
            string trackedValue = initialValue ?? string.Empty;
            var textField = new TextField(label)
            {
                value = trackedValue,
                isDelayed = true
            };

            textField.RegisterValueChangedCallback(evt =>
            {
                string newValue = evt.newValue ?? string.Empty;
                if (string.Equals(trackedValue, newValue, StringComparison.Ordinal))
                    return;

                Undo.RecordObject(target, label);
                property.stringValue = newValue;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                onRenamed?.Invoke(trackedValue, newValue);
                trackedValue = newValue;
                onChanged?.Invoke();
                QueueExternalNotify(notifyFlags);
            });
            root.Add(textField);
        }

        private void BindField(VisualElement root, SerializedProperty property, string label, Action onChanged)
        {
            var field = new PropertyField(property.Copy(), label);
            field.Bind(serializedObject);
            if (onChanged != null)
                field.RegisterValueChangeCallback(_ => onChanged.Invoke());
            root.Add(field);
        }

        private void BindCanvasGroupField(VisualElement root, SerializedProperty groupIdProp, Action onChanged)
        {
            var settings = (UILayerSettings)target;
            PopupField<string> popup = UISystemEditorCanvasGroups.CreatePopupField(groupIdProp, settings);
            popup.RegisterValueChangedCallback(evt =>
            {
                string newGroupId = evt.newValue;
                if (string.Equals(groupIdProp.stringValue, newGroupId, StringComparison.Ordinal))
                    return;

                groupIdProp.stringValue = newGroupId;
                serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            });

            openLayerCanvasGroupPopup = popup;
            openLayerCanvasGroupPropPath = groupIdProp.propertyPath;
            root.Add(popup);
        }

        private void RefreshCanvasGroupListRow(int index)
        {
            if (!canvasGroupRowBindings.TryGetValue(index, out CanvasGroupListRowBinding binding))
                return;

            RefreshCanvasGroupListRow(binding);
        }

        private void RefreshCanvasGroupListRow(CanvasGroupListRowBinding binding)
        {
            if (binding == null)
                return;

            serializedObject.Update();
            SerializedProperty groups = serializedObject.FindProperty("canvasGroups");
            if (binding.Index < 0 || binding.Index >= groups.arraySize)
                return;

            SerializedProperty element = groups.GetArrayElementAtIndex(binding.Index);
            binding.TitleLabel.text = element.FindPropertyRelative("groupId").stringValue;
            binding.SubtitleLabel.text =
                $"order {element.FindPropertyRelative("sortingOrder").intValue} · {element.FindPropertyRelative("canvasName").stringValue}";
        }

        private void RefreshLayerListRow(int index)
        {
            if (!layerRowBindings.TryGetValue(index, out LayerListRowBinding binding))
                return;

            RefreshLayerListRow(binding);
        }

        private void RefreshLayerListRow(LayerListRowBinding binding)
        {
            if (binding == null)
                return;

            var settings = (UILayerSettings)target;
            serializedObject.Update();
            SerializedProperty layers = serializedObject.FindProperty("layers");
            if (binding.Index < 0 || binding.Index >= layers.arraySize)
                return;

            SerializedProperty element = layers.GetArrayElementAtIndex(binding.Index);
            string layerId = element.FindPropertyRelative("layerId").stringValue;
            SerializedProperty canvasGroupIdProp = element.FindPropertyRelative("canvasGroupId");
            string groupId = UISystemEditorCanvasGroups.ResolveGroupId(canvasGroupIdProp);

            binding.TitleLabel.text = layerId;
            binding.SubtitleLabel.text =
                $"{element.FindPropertyRelative("sortOrder").intValue} · {UISystemEditorCanvasGroups.FormatGroupLabel(groupId, settings)}";

            if (binding.BuiltInBadge != null)
            {
                binding.BuiltInBadge.style.display = UISystemBuiltIn.IsBuiltInLayerId(layerId)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (binding.StackBadge != null)
            {
                binding.StackBadge.style.display = element.FindPropertyRelative("useScreenStack").boolValue
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (binding.OpenScreenButton != null)
                ApplyOpenScreenButtonStyle(binding.OpenScreenButton, IsScreenStackLayerId(layerId));
        }

        private void RefreshAllLayerListRows()
        {
            foreach (LayerListRowBinding binding in layerRowBindings.Values)
                RefreshLayerListRow(binding);
        }

        private void RefreshAllLayerListRowSubtitles()
        {
            var settings = (UILayerSettings)target;
            serializedObject.Update();
            SerializedProperty layers = serializedObject.FindProperty("layers");

            foreach (KeyValuePair<int, LayerListRowBinding> pair in layerRowBindings)
            {
                LayerListRowBinding binding = pair.Value;
                if (binding.Index < 0 || binding.Index >= layers.arraySize)
                    continue;

                SerializedProperty element = layers.GetArrayElementAtIndex(binding.Index);
                SerializedProperty canvasGroupIdProp = element.FindPropertyRelative("canvasGroupId");
                string groupId = UISystemEditorCanvasGroups.ResolveGroupId(canvasGroupIdProp);
                binding.SubtitleLabel.text =
                    $"{element.FindPropertyRelative("sortOrder").intValue} · {UISystemEditorCanvasGroups.FormatGroupLabel(groupId, settings)}";
            }
        }

        private void SyncOpenLayerDetailCanvasGroupPopup()
        {
            if (openLayerCanvasGroupPopup == null || string.IsNullOrEmpty(openLayerCanvasGroupPropPath))
                return;

            serializedObject.Update();
            SerializedProperty groupIdProp = serializedObject.FindProperty(openLayerCanvasGroupPropPath);
            if (groupIdProp == null)
                return;

            UISystemEditorCanvasGroups.SyncPopupField(
                openLayerCanvasGroupPopup,
                groupIdProp,
                (UILayerSettings)target);
        }

        private void SyncScreenStackLayerPopup()
        {
            if (screenStackLayerPopup == null)
                return;

            serializedObject.Update();
            SerializedProperty layers = serializedObject.FindProperty("layers");
            var choices = new List<string>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                string layerId = layers.GetArrayElementAtIndex(i).FindPropertyRelative("layerId").stringValue;
                if (!string.IsNullOrEmpty(layerId) && !choices.Contains(layerId))
                    choices.Add(layerId);
            }

            if (choices.Count == 0)
            {
                screenStackLayerPopup.style.display = DisplayStyle.None;
                return;
            }

            screenStackLayerPopup.style.display = DisplayStyle.Flex;
            string current = GetScreenStackLayerId(layers);
            if (string.IsNullOrEmpty(current) || !choices.Contains(current))
                current = choices[0];

            suppressScreenStackPopupCallback = true;
            screenStackLayerPopup.choices = choices;
            screenStackLayerPopup.SetValueWithoutNotify(current);
            suppressScreenStackPopupCallback = false;
        }

        private static string GetScreenStackLayerId(SerializedProperty layers)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("useScreenStack").boolValue)
                    return element.FindPropertyRelative("layerId").stringValue;
            }

            return null;
        }

        private bool IsScreenStackLayerId(string layerId)
        {
            if (string.IsNullOrEmpty(layerId))
                return false;

            serializedObject.Update();
            SerializedProperty layers = serializedObject.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (!element.FindPropertyRelative("useScreenStack").boolValue)
                    continue;

                return string.Equals(element.FindPropertyRelative("layerId").stringValue, layerId, StringComparison.Ordinal);
            }

            return false;
        }

        private void EnsureScreenStackLayerAssigned()
        {
            serializedObject.Update();
            SerializedProperty layers = serializedObject.FindProperty("layers");
            if (layers.arraySize == 0)
                return;

            string current = GetScreenStackLayerId(layers);
            if (!string.IsNullOrEmpty(current))
            {
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("layerId").stringValue == current)
                        return;
                }
            }

            string fallback = FindLayerId(layers, UILayers.Screen);
            if (string.IsNullOrEmpty(fallback))
                fallback = layers.GetArrayElementAtIndex(0).FindPropertyRelative("layerId").stringValue;

            ApplyScreenStackLayer(fallback, notify: false);
        }

        private static string FindLayerId(SerializedProperty layers, string layerId)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("layerId").stringValue == layerId)
                    return layerId;
            }

            return null;
        }

        private void ApplyScreenStackLayer(string layerId, bool notify = true)
        {
            if (string.IsNullOrEmpty(layerId))
                return;

            Undo.RecordObject(target, "Set OpenScreen Layer");
            serializedObject.Update();
            SerializedProperty layers = serializedObject.FindProperty("layers");
            bool anyMatched = false;
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                bool isTarget = string.Equals(
                    element.FindPropertyRelative("layerId").stringValue,
                    layerId,
                    StringComparison.Ordinal);
                element.FindPropertyRelative("useScreenStack").boolValue = isTarget;
                anyMatched |= isTarget;
            }

            if (!anyMatched)
                return;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            RefreshAllLayerListRows();
            SyncScreenStackLayerPopup();
            if (selectedLayerIndex >= 0 && (layerSelection == null || layerSelection.Count <= 1))
                RebuildLayersDetail();
            if (notify)
                QueueExternalNotify(ExternalNotifyFlags.Layers);
        }
    }
}
