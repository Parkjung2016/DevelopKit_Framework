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

        private Label entriesCountLabel;

        private VisualElement entriesHost;



        public override VisualElement CreateInspectorGUI()

        {

            var root = new VisualElement();



            root.Add(UISystemEditorUI.BuildHeader(

                "UI 뷰 카탈로그",

                "OpenScreen / OpenPopup에 사용할 UI 프리팹을 등록합니다."));



            root.Add(UISystemEditorUI.BuildOpenSettingsToolbar(serializedObject.targetObject));

            var helpFoldout = new Foldout { text = "기본 레이어 · 사용 안내", value = false };

            helpFoldout.style.marginBottom = 8;

            helpFoldout.Add(UISystemEditorUI.BuildBuiltInLayersReference(asFoldout: false));

            helpFoldout.Add(UISystemEditorUI.BuildHint(
                "viewId는 프리팹 루트 이름입니다. layerId는 프리팹 인스펙터에서 지정하거나 비워 두면 베이스 타입 기본값을 씁니다.\n" +
                "Addressable 주소는 에셋 주소가 다를 때만 입력합니다. 비우면 뷰 ID를 사용합니다."));

            root.Add(helpFoldout);



            var actionPanel = UISystemEditorUI.BuildActionPanel();

            actionPanel.Add(UISystemEditorUI.BuildToolbar(

                ("항목 추가", () =>

                {

                    SerializedProperty entries = serializedObject.FindProperty("entries");

                    entries.InsertArrayElementAtIndex(entries.arraySize);

                    serializedObject.ApplyModifiedProperties();

                    RebuildEntries();

                }),

                ("선택 프리팹 추가", AddSelectedPrefab),

                ("이름순 정렬", SortEntriesByViewId)));

            actionPanel.Add(UISystemEditorUI.BuildDragDropZone(

                "UIViewBase 프리팹을 여기로 드래그하여 추가",

                dropped =>

                {

                    for (int i = 0; i < dropped.Count; i++)

                        TryAddPrefab(dropped[i]);

                }));

            root.Add(actionPanel);



            entriesCountLabel = UISystemEditorUI.BuildSectionLabel("등록된 뷰");

            root.Add(entriesCountLabel);



            var scroll = new ScrollView(ScrollViewMode.Vertical);

            scroll.style.flexGrow = 1;
            if (!UISystemEditorUI.PreferWindowLayout)
            {
                scroll.style.minHeight = 120;
                scroll.style.maxHeight = 520;
            }

            scroll.style.marginBottom = 4;

            entriesHost = new VisualElement();

            scroll.Add(entriesHost);

            root.Add(scroll);



            RebuildEntries();

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged += RebuildEntries;
                EditorApplication.projectChanged += RebuildEntries;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged -= RebuildEntries;
                EditorApplication.projectChanged -= RebuildEntries;
            });

            return root;

        }



        private void AddSelectedPrefab()

        {

            GameObject selected = Selection.activeGameObject;

            if (selected == null)

            {

                EditorUtility.DisplayDialog("UI 뷰 카탈로그", "UIViewBase가 붙은 프리팹 또는 오브젝트를 선택하세요.", "확인");

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

                EditorUtility.DisplayDialog("UI 뷰 카탈로그", "드래그로 추가할 수 있는 것은 프리팹 에셋뿐입니다.", "확인");

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

            serializedObject.ApplyModifiedProperties();

            RebuildEntries();

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

                    if (string.Compare(a, b, System.StringComparison.Ordinal) > 0)

                        entries.MoveArrayElement(j, i);

                }

            }



            serializedObject.ApplyModifiedProperties();

            RebuildEntries();

        }



        private void RebuildEntries()

        {

            entriesHost.Clear();

            serializedObject.Update();



            SerializedProperty entries = serializedObject.FindProperty("entries");

            entriesCountLabel.text = $"등록된 뷰 ({entries.arraySize})";



            if (entries.arraySize == 0)

            {

                entriesHost.Add(UISystemEditorUI.BuildHelpBox(

                    "등록된 뷰가 없습니다. 프리팹을 드래그하거나 '항목 추가'를 사용하세요.",

                    HelpBoxMessageType.Warning));

                return;

            }



            for (int i = 0; i < entries.arraySize; i++)

                entriesHost.Add(BuildEntryCard(entries.GetArrayElementAtIndex(i), i, entries));

        }



        private VisualElement BuildEntryCard(SerializedProperty element, int index, SerializedProperty entriesArray)

        {

            SerializedProperty prefabProp = element.FindPropertyRelative("prefab");

            SerializedProperty viewIdProp = element.FindPropertyRelative("viewId");

            SerializedProperty loadProp = element.FindPropertyRelative("loadFromAddressable");

            SerializedProperty addressableKeyProp = element.FindPropertyRelative("addressableKey");



            var prefab = prefabProp.objectReferenceValue as UIViewBase;

            string viewId = GetEntryViewId(element);

            string layerId = UISystemEditorLayers.FormatLayerIdLabel(prefab);

            string typeName = prefab != null ? prefab.GetType().Name : "프리팹 없음";

            var card = UISystemEditorUI.BuildCard();



            var header = new VisualElement();

            header.style.flexDirection = FlexDirection.Row;

            header.style.alignItems = Align.Center;

            header.style.flexWrap = Wrap.Wrap;

            header.style.marginBottom = 6;



            var title = new Label(viewId);

            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            title.style.fontSize = 13;

            title.style.marginRight = 6;

            header.Add(title);



            var typeBadge = UISystemEditorUI.BuildBadge(typeName, new Color(0.75f, 0.75f, 0.75f));

            header.Add(typeBadge);



            var layerBadge = UISystemEditorUI.BuildBadge($"레이어 {layerId}", new Color(0.6f, 0.85f, 1f));
            header.Add(layerBadge);

            var addressableBadgeHost = new VisualElement();
            addressableBadgeHost.style.flexDirection = FlexDirection.Row;
            header.Add(addressableBadgeHost);

            var spacer = new VisualElement();

            spacer.style.flexGrow = 1;

            header.Add(spacer);



            var ping = new Button(() =>

            {

                if (prefab != null)

                    EditorGUIUtility.PingObject(prefab);

            })

            { text = "찾기" };

            ping.style.height = 20;

            ping.style.marginRight = 4;

            header.Add(ping);



            var remove = new Button(() =>

            {

                entriesArray.DeleteArrayElementAtIndex(index);

                serializedObject.ApplyModifiedProperties();

                RebuildEntries();

            })

            { text = "삭제" };

            remove.style.height = 20;

            header.Add(remove);



            card.Add(header);



            var basics = UISystemEditorUI.BuildFieldGroup("기본");
            UISystemEditorUI.BindProperty(basics, prefabProp, serializedObject, "프리팹");

            if (prefab != null)
            {
                basics.Add(UISystemEditorUI.BuildHint(
                    $"viewId: {prefab.ViewId} · layerId: {UISystemEditorLayers.FormatLayerIdLabel(prefab)}"));
            }
            else
            {
                UISystemEditorUI.BindProperty(basics, viewIdProp, serializedObject, "뷰 ID");
                basics.Add(UISystemEditorUI.BuildHint("프리팹 없이 Addressable만 등록할 때 입력합니다."));
            }

            card.Add(basics);



            var addressableGroup = UISystemEditorUI.BuildFieldGroup("Addressable (선택)");

            var loadField = new PropertyField(loadProp.Copy(), "Addressable로 로드");
            loadField.Bind(serializedObject);
            addressableGroup.Add(loadField);

            addressableGroup.Add(UISystemEditorUI.BuildHint(
                "켜면 프리팹 참조 대신 Addressable에서 로드합니다. 프리팹 없이 주소만으로 등록할 수도 있습니다."));

            var addressableKeyRow = new VisualElement();
            UISystemEditorUI.BindProperty(addressableKeyRow, addressableKeyProp, serializedObject, "Addressable 주소");
            addressableKeyRow.Add(UISystemEditorUI.BuildHint("비우면 뷰 ID를 주소로 사용합니다."));
            addressableGroup.Add(addressableKeyRow);

            void RefreshAddressableDisplay()
            {
                serializedObject.Update();

                bool showAddressable = loadProp.boolValue || prefabProp.objectReferenceValue == null;
                addressableKeyRow.style.display = showAddressable ? DisplayStyle.Flex : DisplayStyle.None;

                addressableBadgeHost.Clear();
                if (showAddressable)
                {
                    addressableBadgeHost.Add(UISystemEditorUI.BuildBadge(
                        "Addressable",
                        new Color(0.85f, 0.75f, 0.45f)));
                }
            }

            RefreshAddressableDisplay();

            loadField.RegisterValueChangeCallback(_ => RefreshAddressableDisplay());
            card.TrackPropertyValue(loadProp, _ => RefreshAddressableDisplay());

            card.TrackPropertyValue(prefabProp, prop =>
            {
                if (prop.objectReferenceValue != null)
                    viewIdProp.stringValue = string.Empty;

                RefreshAddressableDisplay();
                RebuildEntries();
            });

            card.Add(addressableGroup);
            return card;
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


