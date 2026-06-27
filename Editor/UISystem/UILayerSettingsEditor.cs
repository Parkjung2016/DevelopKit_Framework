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
        private VisualElement rootElement;
        private VisualElement canvasGroupsHost;
        private VisualElement layersHost;
        private VisualElement subTabRow;
        private readonly List<Action> layerSummaryRefreshers = new();
        private bool externalNotifyScheduled;

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
            }

            if (!UISystemEditorUI.PreferWindowLayout)
                rootElement.Add(UISystemEditorUI.BuildOpenSettingsToolbar(serializedObject.targetObject));

            if (UISystemEditorUI.PreferWindowLayout)
            {
                if (ActiveSection == UISystemEditorUI.LayerSettingsSection.CanvasGroups)
                {
                    canvasGroupsHost = BuildCanvasGroupsPanel(settings);
                    rootElement.Add(canvasGroupsHost);
                }
                else
                {
                    layersHost = BuildLayersPanel(settings);
                    rootElement.Add(layersHost);
                }
            }
            else
            {
                canvasGroupsHost = BuildCanvasGroupsPanel(settings);
                layersHost = BuildLayersPanel(settings);
                rootElement.Add(canvasGroupsHost);
                rootElement.Add(layersHost);
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
            if (canvasGroupsHost != null)
                canvasGroupsHost.style.display = showCanvas ? DisplayStyle.Flex : DisplayStyle.None;
            if (layersHost != null)
                layersHost.style.display = showCanvas ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement BuildCanvasGroupsPanel(UILayerSettings settings)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;

            panel.Add(UISystemEditorUI.BuildToolbar(
                ("기본값으로 초기화", () => ResetAll(settings)),
                ("빈 묶음 추가", AddEmptyCanvasGroup),
                ("기본 묶음 추가…", ShowAddBuiltInCanvasGroupMenu)));

            var scroll = CreateListScroll();
            var list = new VisualElement();
            scroll.Add(list);
            panel.Add(scroll);

            RebuildCanvasGroupsList(list);
            return panel;
        }

        private VisualElement BuildLayersPanel(UILayerSettings settings)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;

            panel.Add(UISystemEditorUI.BuildToolbar(
                ("기본값으로 초기화", () => ResetAll(settings)),
                ("빈 레이어 추가", AddEmptyLayer),
                ("기본 레이어 추가…", ShowAddBuiltInMenu)));

            var reference = UISystemEditorUI.BuildBuiltInLayersReference();
            reference.style.marginBottom = 6;
            panel.Add(reference);

            var scroll = CreateListScroll();
            var list = new VisualElement();
            scroll.Add(list);
            panel.Add(scroll);

            RebuildLayersList(list);
            return panel;
        }

        private static ScrollView CreateListScroll()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            if (!UISystemEditorUI.PreferWindowLayout)
            {
                scroll.style.minHeight = 160;
                scroll.style.maxHeight = 520;
            }

            return scroll;
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
            if (canvasGroupsHost != null && canvasGroupsHost.childCount > 0)
            {
                var scroll = canvasGroupsHost.Q<ScrollView>();
                var list = scroll?[0] as VisualElement;
                if (list != null)
                    RebuildCanvasGroupsList(list);
            }

            if (layersHost != null && layersHost.childCount > 0)
            {
                var scroll = layersHost.Q<ScrollView>();
                var list = scroll?[0] as VisualElement;
                if (list != null)
                    RebuildLayersList(list);
            }
        }

        private void RebuildCanvasGroupsList(VisualElement listHost)
        {
            listHost.Clear();
            serializedObject.Update();

            SerializedProperty groups = serializedObject.FindProperty("canvasGroups");
            if (groups.arraySize == 0)
            {
                listHost.Add(UISystemEditorUI.BuildHelpBox(
                    "Canvas 묶음이 없습니다. '기본값으로 초기화' 또는 '빈 묶음 추가'를 사용하세요.",
                    HelpBoxMessageType.Warning));
                return;
            }

            for (int i = 0; i < groups.arraySize; i++)
                listHost.Add(BuildCanvasGroupCard(groups.GetArrayElementAtIndex(i), i, groups));
        }

        private void RebuildLayersList(VisualElement listHost)
        {
            listHost.Clear();
            layerSummaryRefreshers.Clear();
            serializedObject.Update();

            SerializedProperty layers = serializedObject.FindProperty("layers");
            if (layers.arraySize == 0)
            {
                listHost.Add(UISystemEditorUI.BuildHelpBox(
                    "레이어가 없습니다. '기본값으로 초기화' 또는 '빈 레이어 추가'를 사용하세요.",
                    HelpBoxMessageType.Warning));
                return;
            }

            for (int i = 0; i < layers.arraySize; i++)
                listHost.Add(BuildLayerCard(layers.GetArrayElementAtIndex(i), i, layers));
        }

        private void OnExternalCanvasGroupsChanged() => RefreshAllLayerSummaries();

        private void RefreshAllLayerSummaries()
        {
            for (int i = 0; i < layerSummaryRefreshers.Count; i++)
                layerSummaryRefreshers[i]?.Invoke();
        }

        private void QueueExternalNotify()
        {
            if (externalNotifyScheduled || rootElement?.panel == null)
                return;

            externalNotifyScheduled = true;
            rootElement.schedule.Execute(() =>
            {
                externalNotifyScheduled = false;
                UISystemEditorLayers.NotifyLayerIdChanged();
                UISystemEditorCanvasGroups.NotifyCanvasGroupsChanged();
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
            RebuildActiveLists();
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
            RebuildActiveLists();
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
            RebuildActiveLists();
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
            RebuildActiveLists();
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

        private VisualElement BuildCanvasGroupCard(SerializedProperty element, int index, SerializedProperty groupsArray)
        {
            SerializedProperty groupIdProp = element.FindPropertyRelative("groupId");
            SerializedProperty sortingOrderProp = element.FindPropertyRelative("sortingOrder");

            var card = UISystemEditorUI.BuildCard();

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            var title = new Label();
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13;
            title.style.flexGrow = 1;
            header.Add(title);

            var badgeHost = new VisualElement();
            badgeHost.style.flexDirection = FlexDirection.Row;
            header.Add(badgeHost);

            var remove = new Button(() =>
            {
                groupsArray.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                RebuildActiveLists();
                QueueExternalNotify();
            })
            { text = "삭제" };
            remove.style.height = 20;
            header.Add(remove);
            card.Add(header);

            var description = UISystemEditorUI.BuildHint(string.Empty);
            card.Add(description);

            void RefreshSummary()
            {
                serializedObject.Update();
                string groupId = groupIdProp.stringValue;
                title.text = $"{groupId}  ·  order {sortingOrderProp.intValue}";

                badgeHost.Clear();
                if (UISystemBuiltIn.IsBuiltInCanvasGroupId(groupId))
                    badgeHost.Add(UISystemEditorUI.BuildBadge("기본", new Color(0.6f, 0.85f, 1f)));

                description.text = UISystemBuiltIn.TryGetCanvasGroup(groupId, out BuiltInCanvasGroupInfo info)
                    ? info.Description
                    : string.Empty;
                description.style.display = string.IsNullOrEmpty(description.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            RefreshSummary();

            var fields = UISystemEditorUI.BuildFieldGroup();
            BindField(fields, groupIdProp, "묶음 ID", () =>
            {
                RefreshSummary();
                RefreshAllLayerSummaries();
                QueueExternalNotify();
            });
            BindField(fields, element.FindPropertyRelative("displayName"), "표시 이름", QueueExternalNotify);
            BindField(fields, sortingOrderProp, "sortingOrder", () =>
            {
                RefreshSummary();
                QueueExternalNotify();
            });
            BindField(fields, element.FindPropertyRelative("canvasName"), "Canvas 이름", QueueExternalNotify);
            card.Add(fields);

            return card;
        }

        private VisualElement BuildLayerCard(SerializedProperty element, int index, SerializedProperty layersArray)
        {
            var settings = (UILayerSettings)target;
            SerializedProperty layerIdProp = element.FindPropertyRelative("layerId");
            SerializedProperty sortOrderProp = element.FindPropertyRelative("sortOrder");
            SerializedProperty canvasGroupIdProp = element.FindPropertyRelative("canvasGroupId");

            var card = UISystemEditorUI.BuildCard();

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            var title = new Label();
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13;
            title.style.flexGrow = 1;
            header.Add(title);

            var badgeHost = new VisualElement();
            badgeHost.style.flexDirection = FlexDirection.Row;
            header.Add(badgeHost);

            var remove = new Button(() =>
            {
                layersArray.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                RebuildActiveLists();
                QueueExternalNotify();
            })
            { text = "삭제" };
            remove.style.height = 20;
            header.Add(remove);
            card.Add(header);

            var description = UISystemEditorUI.BuildHint(string.Empty);
            card.Add(description);

            void RefreshSummary()
            {
                serializedObject.Update();

                string layerId = layerIdProp.stringValue;
                int sortOrder = sortOrderProp.intValue;
                string groupId = string.IsNullOrEmpty(canvasGroupIdProp.stringValue)
                    ? UISystemEditorCanvasGroups.ReadLegacyCanvasGroupId(canvasGroupIdProp)
                    : canvasGroupIdProp.stringValue;

                title.text = $"{layerId}  ·  {sortOrder}  ·  {UISystemEditorCanvasGroups.FormatGroupLabel(groupId, settings)}";

                badgeHost.Clear();
                if (UISystemBuiltIn.IsBuiltInLayerId(layerId))
                    badgeHost.Add(UISystemEditorUI.BuildBadge("기본", new Color(0.6f, 0.85f, 1f)));

                description.text = UISystemBuiltIn.TryGetLayer(layerId, out BuiltInLayerInfo info)
                    ? info.Description
                    : string.Empty;
                description.style.display = string.IsNullOrEmpty(description.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            RefreshSummary();
            layerSummaryRefreshers.Add(RefreshSummary);

            var fields = UISystemEditorUI.BuildFieldGroup();
            BindField(fields, layerIdProp, "레이어 ID", () =>
            {
                RefreshSummary();
                QueueExternalNotify();
            });
            BindField(fields, element.FindPropertyRelative("displayName"), "표시 이름", QueueExternalNotify);
            BindField(fields, sortOrderProp, "정렬 순서", RefreshSummary);
            BindCanvasGroupField(fields, canvasGroupIdProp, () =>
            {
                RefreshSummary();
                QueueExternalNotify();
            });
            BindField(fields, element.FindPropertyRelative("useScreenStack"), "화면 스택", QueueExternalNotify);
            BindField(fields, element.FindPropertyRelative("rootName"), "루트 이름", QueueExternalNotify);
            card.Add(fields);

            return card;
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
            var container = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                if (UISystemEditorCanvasGroups.DrawCanvasGroupPopup(
                        groupIdProp,
                        settings,
                        new GUIContent("Canvas 묶음")))
                {
                    onChanged?.Invoke();
                }
            });
            container.style.height = EditorGUIUtility.singleLineHeight + 4;
            root.Add(container);
        }
    }
}
