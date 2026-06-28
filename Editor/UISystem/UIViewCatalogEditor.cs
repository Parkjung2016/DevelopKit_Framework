using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    [CustomEditor(typeof(UIViewCatalog))]
    public sealed class UIViewCatalogEditor : Editor
    {
        private enum KindFilter
        {
            All,
            Screen,
            Popup,
            View,
            Unknown
        }

        private Label entriesCountLabel;
        private VisualElement listHost;
        private VisualElement detailHost;
        private ToolbarSearchField searchField;
        private PopupField<string> kindFilterField;
        private KindFilter kindFilter = KindFilter.All;
        private int selectedIndex => catalogSelection?.Primary ?? -1;
        private UISystemEditorListSelectionController catalogSelection;
        private string searchText = string.Empty;
        private readonly Dictionary<UIViewCatalogEntryKind, bool> kindSectionExpanded = new();
        private int lastDetailKey = int.MinValue;
        private bool suppressDetailPrefabTrack;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;

            if (!UISystemEditorUI.PreferWindowLayout)
            {
                root.Add(UISystemEditorUI.BuildHeader(
                    "UI 뷰 카탈로그",
                    "OpenScreen / OpenPopup에 사용할 UI 프리팹을 등록합니다."));
                root.Add(UISystemEditorUI.BuildOpenSettingsToolbar(serializedObject.targetObject));
            }

            var setupFoldout = new Foldout { text = "추가 · ViewId 생성 · 안내", value = false };
            setupFoldout.style.marginBottom = 6;
            setupFoldout.Add(BuildActionPanel());
            setupFoldout.Add(UIViewIdsScriptGenerator.BuildGenerationPanel((UIViewCatalog)target));

            var helpFoldout = new Foldout { text = "기본 레이어 · 사용 안내", value = false };
            helpFoldout.Add(UISystemEditorUI.BuildBuiltInLayersReference(asFoldout: false));
            helpFoldout.Add(UISystemEditorUI.BuildHint(
                "viewId = 프리팹 루트 이름. Screen / Popup / View 분류는 베이스 타입 기준.\n" +
                "Addressable 주소 = viewId."));
            setupFoldout.Add(helpFoldout);
            root.Add(setupFoldout);

            root.Add(BuildFilterBar());

            entriesCountLabel = UISystemEditorUI.BuildSectionLabel("등록된 뷰");
            root.Add(entriesCountLabel);

            var split = UISystemEditorUI.BuildMasterDetailSplit(out listHost, out detailHost);
            catalogSelection = new UISystemEditorListSelectionController(
                listHost,
                RebuildDetail,
                DeleteSelectedCatalogEntries);
            root.Add(split);

            RebuildAll();

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged += OnExternalRefresh;
                EditorApplication.projectChanged += OnExternalRefresh;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged -= OnExternalRefresh;
                EditorApplication.projectChanged -= OnExternalRefresh;
            });

            return root;
        }

        private VisualElement BuildActionPanel()
        {
            var panel = new VisualElement();
            panel.style.marginBottom = 4;

            panel.Add(UISystemEditorUI.BuildToolbar(
                ("항목 추가", () =>
                {
                    SerializedProperty entries = serializedObject.FindProperty("entries");
                    entries.InsertArrayElementAtIndex(entries.arraySize);
                    serializedObject.ApplyModifiedProperties();
                    catalogSelection?.SelectSingle(entries.arraySize - 1);
                    RebuildAll();
                }),
                ("선택 프리팹 추가", AddSelectedPrefab),
                ("이름순 정렬", SortEntriesByViewId)));

            panel.Add(UISystemEditorUI.BuildDragDropZone(
                "UIViewBase 프리팹 드래그하여 추가",
                dropped =>
                {
                    for (int i = 0; i < dropped.Count; i++)
                        TryAddPrefab(dropped[i]);
                }));

            return panel;
        }

        private VisualElement BuildFilterBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.alignItems = Align.Center;
            bar.style.marginBottom = 6;

            searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1;
            searchField.style.flexShrink = 1;
            searchField.style.minWidth = 100;
            searchField.style.marginRight = 6;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchText = evt.newValue ?? string.Empty;
                RebuildList();
            });
            bar.Add(searchField);

            var filterGroup = new VisualElement();
            filterGroup.style.flexDirection = FlexDirection.Row;
            filterGroup.style.alignItems = Align.Center;
            filterGroup.style.flexShrink = 0;
            filterGroup.style.flexGrow = 0;
            filterGroup.style.marginRight = 6;

            var filterLabel = new Label("분류");
            filterLabel.style.minWidth = 26;
            filterLabel.style.marginRight = 4;
            filterLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            filterGroup.Add(filterLabel);

            kindFilterField = new PopupField<string>(
                new List<string> { "전체", "Screen", "Popup", "View", "미분류" },
                0);
            kindFilterField.style.minWidth = 96;
            kindFilterField.style.flexShrink = 0;
            kindFilterField.RegisterValueChangedCallback(evt =>
            {
                kindFilter = evt.newValue switch
                {
                    "Screen" => KindFilter.Screen,
                    "Popup" => KindFilter.Popup,
                    "View" => KindFilter.View,
                    "미분류" => KindFilter.Unknown,
                    _ => KindFilter.All
                };
                RebuildList();
            });
            filterGroup.Add(kindFilterField);
            bar.Add(filterGroup);

            var clearSelection = new Button(() =>
            {
                catalogSelection?.ClearSelection();
                RebuildDetail();
            })
            { text = "선택 해제" };
            clearSelection.style.height = 22;
            clearSelection.style.flexShrink = 0;
            bar.Add(clearSelection);

            return bar;
        }

        private void OnExternalRefresh() => RebuildAll();

        private void AddSelectedPrefab()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("UI 뷰 카탈로그", "UIViewBase가 붙은 프리팹을 선택하세요.", "확인");
                return;
            }

            TryAddPrefab(selected);
        }

        private void TryAddPrefab(GameObject gameObject)
        {
            UIViewBase view = gameObject.GetComponent<UIViewBase>();
            if (view == null)
                return;

            string path = AssetDatabase.GetAssetPath(gameObject);
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("UI 뷰 카탈로그", "프리팹 에셋만 추가할 수 있습니다.", "확인");
                return;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            UIViewBase prefabView = prefabAsset != null ? prefabAsset.GetComponent<UIViewBase>() : null;
            if (prefabView == null)
                return;

            SerializedProperty entries = serializedObject.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty prefab = entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab");
                if (prefab.objectReferenceValue == prefabView)
                    return;
            }

            entries.InsertArrayElementAtIndex(entries.arraySize);
            SerializedProperty element = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            element.FindPropertyRelative("prefab").objectReferenceValue = prefabView;
            element.FindPropertyRelative("viewId").stringValue = string.Empty;
            element.FindPropertyRelative("loadFromAddressable").boolValue = false;
            EnforceKindDefaults(element, UIViewCatalogEntryClassifier.Classify(prefabView));
            serializedObject.ApplyModifiedProperties();

            catalogSelection?.SelectSingle(entries.arraySize - 1);
            RebuildAll();
        }

        private void DeleteSelectedCatalogEntries()
        {
            if (catalogSelection == null || catalogSelection.Count == 0)
                return;

            SerializedProperty entries = serializedObject.FindProperty("entries");
            UISystemEditorListSelectionDelete.DeleteDescending(
                entries,
                catalogSelection.GetSelectedSnapshot(),
                target,
                "Delete UI Catalog Entries");
            catalogSelection.ClearSelection();
            RebuildAll();
        }

        private void SortEntriesByViewId()
        {
            SerializedProperty entries = serializedObject.FindProperty("entries");
            for (int i = 0; i < entries.arraySize - 1; i++)
            {
                for (int j = i + 1; j < entries.arraySize; j++)
                {
                    string a = GetEntryViewId(entries.GetArrayElementAtIndex(i));
                    string b = GetEntryViewId(entries.GetArrayElementAtIndex(j));
                    if (string.Compare(a, b, StringComparison.Ordinal) > 0)
                        entries.MoveArrayElement(j, i);
                }
            }

            serializedObject.ApplyModifiedProperties();
            catalogSelection?.ClearSelection();
            RebuildAll();
        }

        private void RebuildAll()
        {
            lastDetailKey = int.MinValue;
            RebuildList();
            RebuildDetail();
        }

        private void RebuildList()
        {
            catalogSelection?.ClearListRows();
            serializedObject.Update();

            SerializedProperty entries = serializedObject.FindProperty("entries");
            EnforceAllKindDefaults(entries);

            int total = entries.arraySize;
            int visible = CountVisibleEntries(entries);
            entriesCountLabel.text = visible == total
                ? $"등록된 뷰 ({total})"
                : $"등록된 뷰 ({visible} / {total})";

            if (total == 0)
            {
                listHost.Add(UISystemEditorUI.BuildHelpBox(
                    "등록된 뷰가 없습니다. '추가 · ViewId 생성 · 안내'에서 프리팹을 추가하세요.",
                    HelpBoxMessageType.Warning));
                catalogSelection?.ClearSelection();
                return;
            }

            catalogSelection?.PruneInvalidIndices(total);

            bool useFlatList = !string.IsNullOrEmpty(searchText) || kindFilter != KindFilter.All;

            if (useFlatList)
            {
                for (int i = 0; i < total; i++)
                {
                    if (!MatchesFilter(entries.GetArrayElementAtIndex(i)))
                        continue;

                    listHost.Add(BuildListRow(entries.GetArrayElementAtIndex(i), i, entries));
                }

                if (listHost.childCount == 0)
                {
                    listHost.Add(UISystemEditorUI.BuildHelpBox(
                        "검색/필터 조건에 맞는 뷰가 없습니다.",
                        HelpBoxMessageType.Info));
                }

                catalogSelection?.RefreshAllRowStyles();
                return;
            }

            var grouped = GroupEntries(entries);
            ReadOnlySpan<UIViewCatalogEntryKind> order = UIViewCatalogEntryClassifier.DisplayOrderSpan;
            for (int i = 0; i < order.Length; i++)
            {
                UIViewCatalogEntryKind kind = order[i];
                if (!grouped.TryGetValue(kind, out List<(int index, SerializedProperty element)> items) || items.Count == 0)
                    continue;

                var section = new Foldout
                {
                    text = $"{UIViewCatalogEntryClassifier.GetKindLabel(kind)} ({items.Count})",
                    value = GetSectionExpanded(kind, items.Count)
                };
                section.style.marginTop = 2;
                section.style.marginBottom = 2;
                section.RegisterValueChangedCallback(evt => kindSectionExpanded[kind] = evt.newValue);

                var titleLabel = section.Q<Label>();
                if (titleLabel != null)
                    titleLabel.style.color = UIViewCatalogEntryClassifier.GetKindColor(kind);

                for (int j = 0; j < items.Count; j++)
                    section.Add(BuildListRow(items[j].element, items[j].index, entries));

                listHost.Add(section);
            }

            catalogSelection?.RefreshAllRowStyles();
        }

        private void RebuildDetail()
        {
            int detailKey = BuildDetailKey();
            if (detailKey == lastDetailKey && detailHost.childCount > 0)
                return;

            lastDetailKey = detailKey;
            detailHost.Clear();

            SerializedProperty entries = serializedObject.FindProperty("entries");
            if (catalogSelection != null && catalogSelection.Count > 1)
            {
                detailHost.Add(BuildMultiSelectDetailPanel(catalogSelection.Count));
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= entries.arraySize)
            {
                detailHost.Add(UISystemEditorUI.BuildHelpBox(
                    "목록에서 뷰를 선택하면 상세 설정을 편집할 수 있습니다.",
                    HelpBoxMessageType.Info));
                return;
            }

            SerializedProperty element = entries.GetArrayElementAtIndex(selectedIndex);
            var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
            UIViewCatalogEntryKind kind = UIViewCatalogEntryClassifier.Classify(prefab);

            detailHost.Add(BuildDetailPanel(element, selectedIndex, entries, kind));
        }

        private int BuildDetailKey()
        {
            if (catalogSelection != null && catalogSelection.Count > 1)
            {
                int hash = catalogSelection.Count;
                foreach (int index in catalogSelection.GetSelectedSnapshot())
                    hash = unchecked(hash * 31 + index);

                return -hash;
            }

            return selectedIndex;
        }

        private VisualElement BuildMultiSelectDetailPanel(int selectedCount)
        {
            var panel = UISystemEditorUI.BuildCard();
            panel.style.marginBottom = 0;

            var title = new Label($"{selectedCount}개 선택됨");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.marginBottom = 6;
            panel.Add(title);

            panel.Add(UISystemEditorUI.BuildHint(
                "Ctrl+A 전체 선택 · Delete 삭제 · Shift/Ctrl+클릭·드래그 다중 선택"));

            var section = UISystemEditorUI.BuildFieldGroup("공통 설정");
            section.Add(BuildBulkBoolToggleRow(
                "Addressable 로드",
                "loadFromAddressable",
                _ => true,
                "선택한 항목의 Addressable 로드를 한 번에 바꿉니다."));
            section.Add(BuildBulkBoolToggleRow(
                "풀링",
                "usePooling",
                _ => true,
                "선택한 항목의 풀링을 한 번에 바꿉니다."));
            section.Add(BuildBulkBoolToggleRow(
                "중복 허용",
                "allowMultipleInstances",
                element =>
                {
                    var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
                    return UIViewCatalogEntryClassifier.SupportsMultipleInstances(
                        UIViewCatalogEntryClassifier.Classify(prefab));
                },
                "Screen 항목은 제외됩니다. Popup/View/미분류만 변경됩니다."));
            panel.Add(section);

            return panel;
        }

        private VisualElement BuildBulkBoolToggleRow(
            string label,
            string propertyName,
            Func<SerializedProperty, bool> includeEntry,
            string hint)
        {
            var row = new VisualElement();
            row.style.marginBottom = 6;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            row.Add(header);

            var title = new Label(label);
            title.style.flexGrow = 1;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            header.Add(title);

            var toggle = new Toggle();
            toggle.showMixedValue = true;
            header.Add(toggle);

            row.Add(UISystemEditorUI.BuildHint(hint));

            RefreshToggleState();

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (toggle.showMixedValue)
                    toggle.showMixedValue = false;

                ApplyBulkBool(propertyName, evt.newValue, includeEntry);
            });

            return row;

            void RefreshToggleState()
            {
                bool? aggregate = AggregateSelectedBool(propertyName, includeEntry);
                if (!aggregate.HasValue)
                {
                    toggle.showMixedValue = true;
                    toggle.SetValueWithoutNotify(false);
                    return;
                }

                toggle.showMixedValue = false;
                toggle.SetValueWithoutNotify(aggregate.Value);
            }
        }

        private bool? AggregateSelectedBool(
            string propertyName,
            Func<SerializedProperty, bool> includeEntry)
        {
            if (catalogSelection == null || catalogSelection.Count == 0)
                return null;

            serializedObject.Update();
            SerializedProperty entries = serializedObject.FindProperty("entries");
            bool? aggregate = null;

            foreach (int index in catalogSelection.GetSelectedSnapshot())
            {
                if (index < 0 || index >= entries.arraySize)
                    continue;

                SerializedProperty element = entries.GetArrayElementAtIndex(index);
                if (!includeEntry(element))
                    continue;

                bool value = element.FindPropertyRelative(propertyName).boolValue;
                if (!aggregate.HasValue)
                    aggregate = value;
                else if (aggregate.Value != value)
                    return null;
            }

            return aggregate;
        }

        private void ApplyBulkBool(
            string propertyName,
            bool value,
            Func<SerializedProperty, bool> includeEntry)
        {
            if (catalogSelection == null || catalogSelection.Count == 0)
                return;

            Undo.RecordObject(target, "Bulk Edit UI Catalog");
            serializedObject.Update();
            SerializedProperty entries = serializedObject.FindProperty("entries");

            foreach (int index in catalogSelection.GetSelectedSnapshot())
            {
                if (index < 0 || index >= entries.arraySize)
                    continue;

                SerializedProperty element = entries.GetArrayElementAtIndex(index);
                if (!includeEntry(element))
                    continue;

                element.FindPropertyRelative(propertyName).boolValue = value;
                var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
                EnforceKindDefaults(element, UIViewCatalogEntryClassifier.Classify(prefab));
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            lastDetailKey = int.MinValue;
            RebuildList();
            RebuildDetail();
        }

        private VisualElement BuildListRow(SerializedProperty element, int index, SerializedProperty entriesArray)
        {
            var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
            UIViewCatalogEntryKind kind = UIViewCatalogEntryClassifier.Classify(prefab);
            string viewId = GetEntryViewId(element);
            bool isSelected = catalogSelection != null && catalogSelection.IsSelected(index);

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
            UISystemEditorListSelectionStyles.PrepareRow(row, isSelected);

            var kindStrip = new VisualElement();
            kindStrip.style.width = 3;
            kindStrip.style.alignSelf = Align.Stretch;
            kindStrip.style.marginRight = 6;
            kindStrip.style.backgroundColor = UIViewCatalogEntryClassifier.GetKindColor(kind);
            kindStrip.style.borderTopLeftRadius = 2;
            kindStrip.style.borderBottomLeftRadius = 2;
            row.Add(kindStrip);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;
            textCol.style.minWidth = 0;

            var title = new Label(viewId);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(title);

            string sub = BuildRowSubtitle(element, prefab, kind);
            if (!string.IsNullOrEmpty(sub))
            {
                var subLabel = new Label(sub);
                subLabel.style.fontSize = 9;
                subLabel.style.color = new Color(0.65f, 0.65f, 0.65f);
                subLabel.style.overflow = Overflow.Hidden;
                subLabel.style.textOverflow = TextOverflow.Ellipsis;
                textCol.Add(subLabel);
            }

            row.Add(textCol);

            var badges = new VisualElement();
            badges.style.flexDirection = FlexDirection.Row;
            badges.style.flexShrink = 0;

            if (element.FindPropertyRelative("loadFromAddressable").boolValue || prefab == null)
                badges.Add(UISystemEditorUI.BuildBadge("ADDR", new Color(0.85f, 0.75f, 0.45f)));

            if (element.FindPropertyRelative("allowMultipleInstances").boolValue)
                badges.Add(UISystemEditorUI.BuildBadge("×N", new Color(0.62f, 0.92f, 0.62f)));

            if (!element.FindPropertyRelative("usePooling").boolValue)
                badges.Add(UISystemEditorUI.BuildBadge("!풀", new Color(0.95f, 0.6f, 0.45f)));

            row.Add(badges);

            var ping = new Button(() =>
            {
                if (prefab != null)
                    EditorGUIUtility.PingObject(prefab);
            })
            { text = "↗" };
            ping.style.width = 22;
            ping.style.height = 18;
            ping.style.marginLeft = 2;
            ping.style.fontSize = 10;
            ping.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            ping.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            row.Add(ping);

            return row;
        }

        private bool GetSectionExpanded(UIViewCatalogEntryKind kind, int itemCount)
        {
            if (!kindSectionExpanded.TryGetValue(kind, out bool expanded))
            {
                expanded = itemCount <= 8;
                kindSectionExpanded[kind] = expanded;
            }

            return expanded;
        }

        private static string BuildRowSubtitle(SerializedProperty element, UIViewBase prefab, UIViewCatalogEntryKind kind)
        {
            string layer = UISystemEditorLayers.FormatLayerIdLabel(prefab);
            string typeName = prefab != null ? prefab.GetType().Name : "Addressable";
            return $"{UIViewCatalogEntryClassifier.GetKindLabel(kind)} · {typeName} · {layer}";
        }

        private VisualElement BuildDetailPanel(
            SerializedProperty element,
            int index,
            SerializedProperty entriesArray,
            UIViewCatalogEntryKind kind)
        {
            SerializedProperty prefabProp = element.FindPropertyRelative("prefab");
            SerializedProperty viewIdProp = element.FindPropertyRelative("viewId");
            SerializedProperty loadProp = element.FindPropertyRelative("loadFromAddressable");
            SerializedProperty poolingProp = element.FindPropertyRelative("usePooling");
            SerializedProperty duplicateProp = element.FindPropertyRelative("allowMultipleInstances");

            var prefab = prefabProp.objectReferenceValue as UIViewBase;
            string viewId = GetEntryViewId(element);

            var panel = UISystemEditorUI.BuildCard();
            panel.style.marginBottom = 0;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label(viewId);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.flexGrow = 1;
            header.Add(title);

            header.Add(UISystemEditorUI.BuildBadge(
                UIViewCatalogEntryClassifier.GetKindLabel(kind),
                UIViewCatalogEntryClassifier.GetKindColor(kind)));

            var addressableBadgeHost = new VisualElement();
            addressableBadgeHost.style.flexDirection = FlexDirection.Row;
            header.Add(addressableBadgeHost);

            var remove = new Button(() =>
            {
                entriesArray.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                if (selectedIndex >= entriesArray.arraySize)
                    catalogSelection?.ClearSelection();
                RebuildAll();
            })
            { text = "삭제" };
            remove.style.height = 20;
            header.Add(remove);
            panel.Add(header);

            var basics = UISystemEditorUI.BuildFieldGroup("기본");
            UISystemEditorUI.BindProperty(basics, prefabProp, serializedObject, "프리팹");

            var basicsExtraHost = new VisualElement();
            basics.Add(basicsExtraHost);
            panel.Add(basics);

            var prefabSettingsHost = new VisualElement();
            panel.Add(prefabSettingsHost);

            void RefreshPrefabDependentUi()
            {
                serializedObject.Update();
                var currentPrefab = prefabProp.objectReferenceValue as UIViewBase;
                UIViewCatalogEntryKind currentKind = UIViewCatalogEntryClassifier.Classify(currentPrefab);

                basicsExtraHost.Clear();
                if (currentPrefab != null)
                {
                    basicsExtraHost.Add(UISystemEditorUI.BuildHint(
                        $"viewId: {currentPrefab.ViewId} · {UIViewCatalogEntryClassifier.GetOpenApiHint(currentKind)}"));
                }
                else
                {
                    UISystemEditorUI.BindProperty(basicsExtraHost, viewIdProp, serializedObject, "뷰 ID");
                    basicsExtraHost.Add(UISystemEditorUI.BuildHint("프리팹 없이 Addressable만 등록할 때 입력합니다."));
                }

                prefabSettingsHost.Clear();
                prefabSettingsHost.Add(UISystemEditorViewPrefabFields.BuildSection(
                    currentPrefab,
                    () => RebuildList()));
            }

            RefreshPrefabDependentUi();

            var loadGroup = UISystemEditorUI.BuildFieldGroup("로드");
            var loadField = new PropertyField(loadProp.Copy(), "Addressable로 로드");
            loadField.Bind(serializedObject);
            loadGroup.Add(loadField);
            loadGroup.Add(UISystemEditorUI.BuildHint("Addressable 주소 = viewId."));
            panel.Add(loadGroup);

            panel.Add(BuildInstanceSection(kind, poolingProp, duplicateProp));

            void RefreshBadges()
            {
                serializedObject.Update();
                addressableBadgeHost.Clear();
                if (loadProp.boolValue || prefabProp.objectReferenceValue == null)
                {
                    addressableBadgeHost.Add(UISystemEditorUI.BuildBadge(
                        "Addressable",
                        new Color(0.85f, 0.75f, 0.45f)));
                }
            }

            RefreshBadges();

            loadField.RegisterValueChangeCallback(_ =>
            {
                RefreshBadges();
                RebuildList();
            });

            panel.TrackPropertyValue(loadProp, _ =>
            {
                RefreshBadges();
                RebuildList();
            });

            suppressDetailPrefabTrack = true;
            panel.TrackPropertyValue(prefabProp, prop =>
            {
                if (suppressDetailPrefabTrack)
                    return;

                if (prop.objectReferenceValue != null)
                    viewIdProp.stringValue = string.Empty;

                EnforceKindDefaults(
                    element,
                    UIViewCatalogEntryClassifier.Classify(prop.objectReferenceValue as UIViewBase));
                serializedObject.ApplyModifiedProperties();
                RefreshBadges();

                UIViewCatalogEntryKind newKind =
                    UIViewCatalogEntryClassifier.Classify(prop.objectReferenceValue as UIViewBase);
                if (newKind != kind)
                {
                    lastDetailKey = int.MinValue;
                    RebuildDetail();
                    RebuildList();
                    return;
                }

                RefreshPrefabDependentUi();
                RebuildList();
            });
            panel.schedule.Execute(() => suppressDetailPrefabTrack = false);

            panel.TrackPropertyValue(poolingProp, _ => RebuildList());
            panel.TrackPropertyValue(duplicateProp, prop =>
            {
                EnforceKindDefaults(element, UIViewCatalogEntryClassifier.Classify(prefabProp.objectReferenceValue as UIViewBase));
                RebuildList();
            });

            return panel;
        }

        private VisualElement BuildInstanceSection(
            UIViewCatalogEntryKind kind,
            SerializedProperty poolingProp,
            SerializedProperty duplicateProp)
        {
            var section = UISystemEditorUI.BuildFieldGroup("인스턴스");
            section.Add(UISystemEditorUI.BuildHint(UIViewCatalogEntryClassifier.GetInstancePolicyHint(kind)));

            if (kind == UIViewCatalogEntryKind.Screen)
            {
                UISystemEditorUI.BindProperty(section, poolingProp, serializedObject, "풀링");
                section.Add(UISystemEditorUI.BuildHint(
                    "Close 시 Hide / Destroy. DisposeAll()은 풀링과 관계없이 전부 파괴."));
                return section;
            }

            if (UIViewCatalogEntryClassifier.SupportsMultipleInstances(kind))
            {
                var poolingField = UISystemEditorUI.CreateLabeledField(poolingProp, serializedObject, "풀링");
                var duplicateField = UISystemEditorUI.CreateLabeledField(duplicateProp, serializedObject, "중복 허용");
                section.Add(UISystemEditorUI.BuildInlineFieldsRow(poolingField, duplicateField));
                section.Add(UISystemEditorUI.BuildHint(
                    "풀링: Close 시 Hide + 비활성화 / Destroy.\n" +
                    "중복 허용: 동시에 여러 개. 풀링과 함께 쓰면 닫힌 인스턴스 재사용."));
                return section;
            }

            UISystemEditorUI.BindProperty(section, poolingProp, serializedObject, "풀링");
            return section;
        }

        private int CountVisibleEntries(SerializedProperty entries)
        {
            int count = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (MatchesFilter(entries.GetArrayElementAtIndex(i)))
                    count++;
            }

            return count;
        }

        private bool MatchesFilter(SerializedProperty element)
        {
            var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
            UIViewCatalogEntryKind kind = UIViewCatalogEntryClassifier.Classify(prefab);

            if (kindFilter != KindFilter.All && KindFilterFromKind(kind) != kindFilter)
                return false;

            if (string.IsNullOrEmpty(searchText))
                return true;

            string query = searchText.Trim();
            string viewId = GetEntryViewId(element);
            if (viewId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (prefab != null)
            {
                if (prefab.GetType().Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (UISystemEditorLayers.FormatLayerIdLabel(prefab).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return UIViewCatalogEntryClassifier.GetKindLabel(kind)
                .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static KindFilter KindFilterFromKind(UIViewCatalogEntryKind kind) =>
            kind switch
            {
                UIViewCatalogEntryKind.Screen => KindFilter.Screen,
                UIViewCatalogEntryKind.Popup => KindFilter.Popup,
                UIViewCatalogEntryKind.View => KindFilter.View,
                _ => KindFilter.Unknown
            };

        private static Dictionary<UIViewCatalogEntryKind, List<(int index, SerializedProperty element)>> GroupEntries(
            SerializedProperty entries)
        {
            var grouped = new Dictionary<UIViewCatalogEntryKind, List<(int, SerializedProperty)>>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty element = entries.GetArrayElementAtIndex(i);
                var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
                UIViewCatalogEntryKind kind = UIViewCatalogEntryClassifier.Classify(prefab);

                if (!grouped.TryGetValue(kind, out List<(int, SerializedProperty)> list))
                {
                    list = new List<(int, SerializedProperty)>();
                    grouped[kind] = list;
                }

                list.Add((i, element));
            }

            return grouped;
        }

        private void EnforceAllKindDefaults(SerializedProperty entries)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty element = entries.GetArrayElementAtIndex(i);
                var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
                EnforceKindDefaults(element, UIViewCatalogEntryClassifier.Classify(prefab));
            }
        }

        private void EnforceKindDefaults(SerializedProperty element, UIViewCatalogEntryKind kind)
        {
            if (kind != UIViewCatalogEntryKind.Screen)
                return;

            SerializedProperty duplicateProp = element.FindPropertyRelative("allowMultipleInstances");
            if (!duplicateProp.boolValue)
                return;

            duplicateProp.boolValue = false;
            serializedObject.ApplyModifiedProperties();
        }

        private static string GetEntryViewId(SerializedProperty element)
        {
            string viewId = element.FindPropertyRelative("viewId").stringValue;
            if (!string.IsNullOrEmpty(viewId))
                return viewId;

            var prefab = element.FindPropertyRelative("prefab").objectReferenceValue as UIViewBase;
            return prefab != null ? prefab.ViewId : "(비어 있음)";
        }
    }
}
