using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    public sealed class UISystemSettingsWindow : EditorWindow
    {
        private enum Tab
        {
            Setup = 0,
            CanvasGroups = 1,
            Layers = 2,
            ViewCatalog = 3
        }

        private ObjectField catalogField;
        private ObjectField layerSettingsField;
        private VisualElement navHost;
        private VisualElement contentHost;
        private Editor embeddedEditor;
        private Object embeddedTarget;
        private Tab activeTab = Tab.Setup;
        private UIViewCatalog viewCatalog;
        private UILayerSettings layerSettings;

        [MenuItem("PJDev/UI/Settings")]
        public static void Open()
        {
            var window = GetWindow<UISystemSettingsWindow>();
            window.titleContent = new GUIContent("UI Settings");
            window.minSize = new Vector2(880, 560);
            window.Show();
            window.Focus();
        }

        private void OnDisable()
        {
            UISystemEditorUI.PreferWindowLayout = false;
            DestroyEmbeddedEditor();
        }

        public static void OpenViewCatalog(UIViewCatalog catalog)
        {
            var window = GetWindow<UISystemSettingsWindow>();
            window.titleContent = new GUIContent("UI Settings");
            window.minSize = new Vector2(880, 560);
            window.viewCatalog = catalog;
            UISystemEditorAssets.Remember(catalog);
            window.activeTab = Tab.ViewCatalog;
            if (window.rootVisualElement.childCount > 0)
            {
                window.catalogField?.SetValueWithoutNotify(catalog);
                window.SelectTab(Tab.ViewCatalog);
            }

            window.Show();
            window.Focus();
        }

        public static void OpenLayerSettings(UILayerSettings settings)
        {
            var window = GetWindow<UISystemSettingsWindow>();
            window.titleContent = new GUIContent("UI Settings");
            window.minSize = new Vector2(880, 560);
            window.layerSettings = settings;
            UISystemEditorAssets.Remember(settings);
            window.activeTab = Tab.Layers;
            if (window.rootVisualElement.childCount > 0)
            {
                window.layerSettingsField?.SetValueWithoutNotify(settings);
                window.SelectTab(Tab.Layers);
            }

            window.Show();
            window.Focus();
        }

        public void CreateGUI()
        {
            UISystemEditorUI.PreferWindowLayout = true;
            rootVisualElement.style.flexGrow = 1;

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(root);

            root.Add(BuildToolbar());
            root.Add(BuildBody(out navHost, out contentHost));

            if (viewCatalog == null)
                viewCatalog = UISystemEditorAssets.LoadOrFindCatalog();
            if (layerSettings == null)
                layerSettings = UISystemEditorAssets.LoadOrFindLayerSettings();
            if (activeTab == Tab.Setup)
                activeTab = NormalizeTab((Tab)EditorPrefs.GetInt(UISystemEditorAssets.LastTabPrefsKey, (int)Tab.Setup));

            if (catalogField != null)
                catalogField.SetValueWithoutNotify(viewCatalog);
            if (layerSettingsField != null)
                layerSettingsField.SetValueWithoutNotify(layerSettings);

            BuildNavigation();
            RefreshContent();
        }

        private static Tab NormalizeTab(Tab tab) =>
            tab switch
            {
                Tab.Setup => Tab.Setup,
                Tab.CanvasGroups => Tab.CanvasGroups,
                Tab.Layers => Tab.Layers,
                Tab.ViewCatalog => Tab.ViewCatalog,
                _ => Tab.Setup
            };

        private VisualElement BuildToolbar()
        {
            var toolbar = UISystemEditorUI.BuildActionPanel();
            toolbar.style.marginBottom = 6;
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.alignItems = Align.Center;

            catalogField = new ObjectField("뷰 카탈로그")
            {
                objectType = typeof(UIViewCatalog),
                allowSceneObjects = false
            };
            catalogField.style.flexGrow = 1;
            catalogField.style.minWidth = 180;
            catalogField.style.marginRight = 6;
            catalogField.RegisterValueChangedCallback(evt =>
            {
                viewCatalog = evt.newValue as UIViewCatalog;
                UISystemEditorAssets.Remember(viewCatalog);
                RefreshContent();
            });
            toolbar.Add(catalogField);

            layerSettingsField = new ObjectField("레이어 설정")
            {
                objectType = typeof(UILayerSettings),
                allowSceneObjects = false
            };
            layerSettingsField.style.flexGrow = 1;
            layerSettingsField.style.minWidth = 180;
            layerSettingsField.style.marginRight = 6;
            layerSettingsField.RegisterValueChangedCallback(evt =>
            {
                layerSettings = evt.newValue as UILayerSettings;
                UISystemEditorAssets.Remember(layerSettings);
                RefreshContent();
            });
            toolbar.Add(layerSettingsField);

            toolbar.Add(CreateToolbarButton("새 카탈로그", () =>
            {
                viewCatalog = UISystemEditorAssets.CreateCatalogAsset(GetTargetFolder());
                catalogField.SetValueWithoutNotify(viewCatalog);
                UISystemEditorAssets.Remember(viewCatalog);
                SelectTab(Tab.ViewCatalog);
            }));

            toolbar.Add(CreateToolbarButton("새 레이어", () =>
            {
                layerSettings = UISystemEditorAssets.CreateLayerSettingsAsset(GetTargetFolder());
                layerSettingsField.SetValueWithoutNotify(layerSettings);
                UISystemEditorAssets.Remember(layerSettings);
                SelectTab(Tab.Layers);
            }));

            toolbar.Add(CreateToolbarButton("저장", () =>
                UISystemEditorAssets.SaveAssets(viewCatalog, layerSettings), primary: true));

            toolbar.Add(CreateToolbarButton("ViewId 생성", () =>
                UIViewIdsScriptGenerator.GenerateWithFeedback(viewCatalog)));

            toolbar.Add(CreateToolbarButton("찾기", () =>
                UISystemEditorAssets.Ping(viewCatalog, layerSettings)));

            return toolbar;
        }

        private static VisualElement BuildBody(out VisualElement navigation, out VisualElement content)
        {
            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Row;
            body.style.minHeight = 0;

            navigation = new VisualElement();
            navigation.style.width = 148;
            navigation.style.flexShrink = 0;
            navigation.style.paddingRight = 8;
            navigation.style.borderRightWidth = 1;
            navigation.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            navigation.style.paddingTop = 4;

            content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.minWidth = 0;
            content.style.minHeight = 0;
            content.style.flexDirection = FlexDirection.Column;
            content.style.paddingLeft = 10;
            content.style.paddingTop = 2;

            body.Add(navigation);
            body.Add(content);
            return body;
        }

        private void BuildNavigation()
        {
            navHost.Clear();
            navHost.Add(CreateNavButton("설정", Tab.Setup));
            navHost.Add(CreateNavButton("Canvas 묶음", Tab.CanvasGroups));
            navHost.Add(CreateNavButton("레이어", Tab.Layers));
            navHost.Add(CreateNavButton("뷰 카탈로그", Tab.ViewCatalog));
        }

        private Button CreateNavButton(string label, Tab tab)
        {
            var button = new Button(() => SelectTab(tab)) { text = label };
            button.name = $"nav-{(int)tab}";
            button.style.height = 28;
            button.style.marginBottom = 4;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 10;
            UpdateNavButtonStyle(button, tab == activeTab);
            return button;
        }

        private void SelectTab(Tab tab)
        {
            activeTab = tab;
            EditorPrefs.SetInt(UISystemEditorAssets.LastTabPrefsKey, (int)tab);
            BuildNavigation();
            RefreshContent();
        }

        private void UpdateNavStyles()
        {
            for (int i = 0; i < navHost.childCount; i++)
            {
                if (navHost[i] is not Button button)
                    continue;

                string name = button.name;
                if (!name.StartsWith("nav-"))
                    continue;

                Tab tab = (Tab)int.Parse(name.Substring("nav-".Length));
                UpdateNavButtonStyle(button, tab == activeTab);
            }
        }

        private static void UpdateNavButtonStyle(Button button, bool selected)
        {
            if (selected)
            {
                button.style.backgroundColor = new Color(0.2f, 0.45f, 0.7f, 0.35f);
                button.style.color = new Color(0.9f, 0.95f, 1f);
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                button.style.backgroundColor = new Color(0f, 0f, 0f, 0.08f);
                button.style.color = StyleKeyword.Null;
                button.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        private void RefreshContent()
        {
            DestroyEmbeddedEditor();
            contentHost.Clear();

            switch (activeTab)
            {
                case Tab.Setup:
                    contentHost.Add(BuildSetupTab());
                    break;
                case Tab.CanvasGroups:
                    UISystemEditorUI.LayerSettingsSectionMode = UISystemEditorUI.LayerSettingsSection.CanvasGroups;
                    ShowEmbeddedEditor(layerSettings, "레이어 설정 에셋을 선택하거나 '새 레이어'로 만드세요.");
                    break;
                case Tab.Layers:
                    UISystemEditorUI.LayerSettingsSectionMode = UISystemEditorUI.LayerSettingsSection.Layers;
                    ShowEmbeddedEditor(layerSettings, "레이어 설정 에셋을 선택하거나 '새 레이어'로 만드세요.");
                    break;
                case Tab.ViewCatalog:
                    ShowEmbeddedEditor(viewCatalog, "뷰 카탈로그 에셋을 선택하거나 '새 카탈로그'로 만드세요.");
                    break;
            }

            UpdateNavStyles();
        }

        private VisualElement BuildSetupTab()
        {
            var root = new VisualElement();

            root.Add(UISystemEditorUI.BuildHeader(
                "UI 시스템 설정",
                "뷰 카탈로그와 레이어 설정을 지정한 뒤 UIManager.Initialize에 연결합니다."));

            var status = UISystemEditorUI.BuildFieldGroup("에셋 상태");
            status.Add(BuildStatusRow("뷰 카탈로그", viewCatalog, () => SelectTab(Tab.ViewCatalog)));
            status.Add(BuildStatusRow("레이어 설정", layerSettings, () => SelectTab(Tab.Layers)));
            status.Add(BuildStatusRow("Canvas 묶음", layerSettings, () => SelectTab(Tab.CanvasGroups)));

            int catalogCount = UISystemEditorAssets.CountAssets<UIViewCatalog>();
            int layerCount = UISystemEditorAssets.CountAssets<UILayerSettings>();
            if (catalogCount > 1 || layerCount > 1)
            {
                status.Add(UISystemEditorUI.BuildHint(
                    $"프로젝트에 UIViewCatalog {catalogCount}개, UILayerSettings {layerCount}개가 있습니다. 상단에서 사용할 에셋을 선택하세요."));
            }

            root.Add(status);

            if (viewCatalog != null)
                root.Add(UIViewIdsScriptGenerator.BuildGenerationPanel(viewCatalog));

            var code = UISystemEditorUI.BuildFieldGroup("런타임 초기화");
            code.Add(UISystemEditorUI.BuildHint(
                "UIManager.Instance.Initialize(viewCatalog, layerSettings);\n" +
                "layerSettings는 생략 가능하며, 생략 시 기본 레이어가 사용됩니다."));
            root.Add(code);

            root.Add(UISystemEditorUI.BuildToolbar(
                ("Canvas 묶음 편집", () => SelectTab(Tab.CanvasGroups)),
                ("레이어 편집", () => SelectTab(Tab.Layers)),
                ("뷰 카탈로그 편집", () => SelectTab(Tab.ViewCatalog)),
                ("ViewId 상수 생성", () => UIViewIdsScriptGenerator.GenerateWithFeedback(viewCatalog)),
                ("둘 다 새로 만들기", CreateDefaultPair)));

            root.Add(UISystemEditorUI.BuildBuiltInLayersReference());
            return root;
        }

        private VisualElement BuildStatusRow(string label, Object asset, System.Action openTab)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            bool assigned = asset != null;
            var badge = UISystemEditorUI.BuildBadge(
                assigned ? "지정됨" : "없음",
                assigned ? new Color(0.5f, 0.9f, 0.55f) : new Color(0.95f, 0.55f, 0.45f));
            row.Add(badge);

            var text = new Label(assigned ? $"{label}: {asset.name}" : $"{label}: 미지정");
            text.style.flexGrow = 1;
            text.style.marginLeft = 6;
            row.Add(text);

            var open = new Button(openTab) { text = "편집" };
            open.style.height = 20;
            row.Add(open);
            return row;
        }

        private void CreateDefaultPair()
        {
            string folder = GetTargetFolder();
            layerSettings = UISystemEditorAssets.CreateLayerSettingsAsset(folder);
            viewCatalog = UISystemEditorAssets.CreateCatalogAsset(folder);
            layerSettingsField.SetValueWithoutNotify(layerSettings);
            catalogField.SetValueWithoutNotify(viewCatalog);
            UISystemEditorAssets.Remember(layerSettings);
            UISystemEditorAssets.Remember(viewCatalog);
            SelectTab(Tab.ViewCatalog);
        }

        private void ShowEmbeddedEditor(Object target, string emptyMessage)
        {
            if (target == null)
            {
                contentHost.Add(UISystemEditorUI.BuildHelpBox(emptyMessage, HelpBoxMessageType.Warning));
                return;
            }

            embeddedTarget = target;
            embeddedEditor = Editor.CreateEditor(target);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.Add(embeddedEditor.CreateInspectorGUI());
            contentHost.Add(scroll);
        }

        private void DestroyEmbeddedEditor()
        {
            if (embeddedEditor != null)
            {
                DestroyImmediate(embeddedEditor);
                embeddedEditor = null;
            }

            embeddedTarget = null;
        }

        private static string GetTargetFolder()
        {
            if (Selection.activeObject != null)
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path))
                {
                    if (AssetDatabase.IsValidFolder(path))
                        return path;

                    string directory = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                        return directory.Replace('\\', '/');
                }
            }

            return "Assets";
        }

        private static Button CreateToolbarButton(string text, System.Action action, bool primary = false)
        {
            var button = new Button(action) { text = text };
            button.style.height = 22;
            button.style.marginRight = 4;
            button.style.marginBottom = 2;
            if (primary)
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }
    }
}
