using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageAssetBrowserPanel : VisualElement
    {
        private readonly MontageEditorContext context;
        private readonly Action createMontage;
        private readonly Action createLibrary;
        private readonly Dictionary<MontageBrowserTab, Button> tabButtons = new();
        private readonly ScrollView scrollView = new(ScrollViewMode.Vertical);
        private readonly ToolbarSearchField searchField = new();
        private readonly VisualElement actionBar = new();
        private MontageBrowserTab activeTab = MontageBrowserTab.Libraries;
        private Type selectedNotifyType;

        public MontageAssetBrowserPanel(MontageEditorContext context, Action createMontage, Action createLibrary)
        {
            this.context = context;
            this.createMontage = createMontage;
            this.createLibrary = createLibrary;
            style.flexShrink = 0;
            style.flexGrow = 1;
            style.minWidth = 180;
            style.minHeight = 0;
            style.overflow = Overflow.Hidden;
            style.borderRightWidth = 1;
            style.borderRightColor = new Color(1f, 1f, 1f, 0.06f);
            style.flexDirection = FlexDirection.Column;
            focusable = true;

            Add(MontageEditorLayoutHelper.CreatePanelHeader("Browser"));

            var tabs = new Toolbar();
            tabs.style.flexShrink = 0;
            tabs.Add(CreateTabButton("Libraries", MontageBrowserTab.Libraries));
            tabs.Add(CreateTabButton("Montages", MontageBrowserTab.Montages));
            Add(tabs);

            searchField.style.flexShrink = 0;
            searchField.RegisterValueChangedCallback(_ => Rebuild());
            Add(searchField);

            actionBar.AddToClassList(AnimMontageEditorStyles.BrowserActionBarClass);
            Add(actionBar);

            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.minHeight = 0;
            Add(scrollView);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= Rebuild);
            context.Changed += Rebuild;
            context.SelectionChanged += Rebuild;
            EditorApplication.projectChanged += Rebuild;
            Rebuild();
        }

        private Button CreateTabButton(string label, MontageBrowserTab tab)
        {
            var button = new Button(() =>
            {
                activeTab = tab;
                Rebuild();
            })
            {
                text = label
            };
            button.AddToClassList(AnimMontageEditorStyles.BrowserTabClass);
            tabButtons[tab] = button;
            return button;
        }

        private void Rebuild()
        {
            scrollView.Clear();
            RebuildActionBar();
            RefreshTabButtons();
            string filter = searchField.value?.Trim() ?? string.Empty;

            switch (activeTab)
            {
                case MontageBrowserTab.Libraries:
                    PopulateAssets<AnimMontageLibrarySO>(filter);
                    break;
                case MontageBrowserTab.Montages:
                    PopulateMontages(filter);
                    break;
            }
        }

        private void RebuildActionBar()
        {
            actionBar.Clear();
            switch (activeTab)
            {
                case MontageBrowserTab.Libraries:
                    actionBar.Add(CreateActionButton("New Library", createLibrary));
                    break;
                case MontageBrowserTab.Montages:
                    Button button = CreateActionButton("New Montage", createMontage);
                    button.SetEnabled(context.MontageLibrary != null);
                    actionBar.Add(button);
                    break;
            }

            actionBar.style.display = actionBar.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshTabButtons()
        {
            foreach (KeyValuePair<MontageBrowserTab, Button> pair in tabButtons)
            {
                pair.Value.EnableInClassList(
                    AnimMontageEditorStyles.BrowserTabActiveClass,
                    pair.Key == activeTab);
            }
        }

        private static Button CreateActionButton(string text, Action onClick)
        {
            var button = new Button(() => onClick?.Invoke())
            {
                text = text
            };
            return button;
        }

        private void PopulateAssets<T>(string filter) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                    continue;

                if (!string.IsNullOrEmpty(filter) &&
                    asset.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                scrollView.Add(CreateRow(asset.name, asset, () =>
                {
                    Focus();
                    selectedNotifyType = null;
                    if (asset is AnimMontageLibrarySO library)
                        context.SetMontageLibrary(library);
                    else if (asset is AnimMontageSO montage)
                        context.SetMontage(montage);
                    else
                        context.SetSelected(asset);
                }));
            }
        }

        private void PopulateMontages(string filter)
        {
            if (context.MontageLibrary == null)
            {
                AddEmptyState("Select a Montage Library.");
                return;
            }

            IReadOnlyList<AnimMontageSO> montages = context.MontageLibrary.Montages;
            for (int i = 0; i < montages.Count; i++)
            {
                AnimMontageSO montage = montages[i];
                if (montage == null)
                    continue;

                if (!string.IsNullOrEmpty(filter) &&
                    montage.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                scrollView.Add(CreateRow(montage.name, montage, () =>
                {
                    Focus();
                    selectedNotifyType = null;
                    context.SetMontage(montage);
                }));
            }
        }

        private void PopulateNotifyTypes<T>(string filter, string category)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsGenericType)
                    continue;

                if (!string.IsNullOrEmpty(filter) &&
                    type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                scrollView.Add(CreateTypeRow($"{category}: {type.Name}", type, () =>
                {
                    Focus();
                    selectedNotifyType = type;
                    context.SetSelected(null);
                }));
            }
        }

        private void PopulateClips(string filter)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                    continue;

                if (!MontageAnimationClipCompatibility.IsCompatible(context.PreviewModel, clip))
                    continue;

                if (!string.IsNullOrEmpty(filter) &&
                    clip.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                scrollView.Add(CreateRow(clip.name, clip, () =>
                {
                    Focus();
                    selectedNotifyType = null;
                    context.SetSelected(clip);
                }));
            }
        }

        private VisualElement CreateRow(string label, UnityEngine.Object asset, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList(AnimMontageEditorStyles.BrowserRowClass);
            if (IsLoadedAsset(asset))
                row.AddToClassList(AnimMontageEditorStyles.BrowserRowLoadedClass);
            if (IsSelectedAsset(asset))
                row.AddToClassList(AnimMontageEditorStyles.BrowserRowSelectedClass);
            row.RegisterCallback<ClickEvent>(_ => onClick());
            if (asset is AnimMontageSO or AnimMontageLibrarySO)
            {
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Rename", _ => RenameAsset(asset));
                    evt.menu.AppendAction("Delete", _ => DeleteAsset(asset));
                }));
            }

            var icon = new Image
            {
                image = AssetPreview.GetMiniThumbnail(asset)
            };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 6;
            row.Add(icon);

            row.Add(new Label(label));
            return row;
        }

        private VisualElement CreateTypeRow(string label, Type type, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList(AnimMontageEditorStyles.BrowserRowClass);
            if (selectedNotifyType == type)
                row.AddToClassList(AnimMontageEditorStyles.BrowserRowSelectedClass);
            row.RegisterCallback<ClickEvent>(_ => onClick());

            row.Add(new Label(label));
            return row;
        }

        private bool IsSelectedAsset(UnityEngine.Object asset)
        {
            return asset != null && asset == context.SelectedObject;
        }

        private bool IsLoadedAsset(UnityEngine.Object asset)
        {
            return asset != null
                   && (asset == context.Montage || asset == context.MontageLibrary);
        }

        private void AddEmptyState(string message)
        {
            var label = new Label(message);
            label.AddToClassList(AnimMontageEditorStyles.EmptyStateClass);
            scrollView.Add(label);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            UnityEngine.Object target = GetKeyboardManagedAsset();
            if (target == null)
                return;

            if (evt.keyCode == KeyCode.F2)
            {
                RenameAsset(target);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Delete)
            {
                DeleteAsset(target);
                evt.StopPropagation();
            }
        }

        private UnityEngine.Object GetKeyboardManagedAsset()
        {
            return activeTab switch
            {
                MontageBrowserTab.Libraries => context.MontageLibrary,
                MontageBrowserTab.Montages => context.Montage,
                _ => null
            };
        }

        private void RenameAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            string title = asset is AnimMontageLibrarySO ? "Rename Montage Library" : "Rename Montage";
            RenameAssetPopup.Show(asset, title, newName =>
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path))
                    return;

                string error = AssetDatabase.RenameAsset(path, newName);
                if (!string.IsNullOrEmpty(error))
                {
                    EditorUtility.DisplayDialog(title, error, "OK");
                    return;
                }

                AssetDatabase.SaveAssets();
                if (asset is AnimMontageLibrarySO library)
                    context.SetMontageLibrary(library);
                else if (asset is AnimMontageSO montage)
                    context.SetMontage(montage);
                Rebuild();
            });
        }

        private void DeleteAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return;

            string assetKind = asset is AnimMontageLibrarySO ? "Montage Library" : "Montage";
            if (!EditorUtility.DisplayDialog(
                    $"Delete {assetKind}",
                    $"Delete '{asset.name}'?\n\n{path}",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            bool deletingCurrentMontage = asset is AnimMontageSO montage && context.Montage == montage;
            bool deletingCurrentLibrary = asset is AnimMontageLibrarySO library && context.MontageLibrary == library;
            bool deletingMontageAsset = asset is AnimMontageSO;
            if (!AssetDatabase.DeleteAsset(path))
            {
                EditorUtility.DisplayDialog($"Delete {assetKind}", "Failed to delete asset.", "OK");
                return;
            }

            if (deletingMontageAsset)
                MontageLibraryReferenceCleaner.RemoveMissingMontageReferences();

            AssetDatabase.SaveAssets();
            if (deletingCurrentMontage)
                context.SetMontage(null);
            else if (deletingCurrentLibrary)
                context.SetMontageLibrary(null);
            else
                Rebuild();
        }

        private sealed class RenameAssetPopup : EditorWindow
        {
            private UnityEngine.Object target;
            private string newName;
            private Action<string> onRename;

            public static void Show(UnityEngine.Object target, string title, Action<string> onRename)
            {
                if (target == null)
                    return;

                var window = CreateInstance<RenameAssetPopup>();
                window.titleContent = new GUIContent(title);
                window.target = target;
                window.newName = target.name;
                window.onRename = onRename;
                window.minSize = new Vector2(300f, 72f);
                window.position = new Rect(220f, 220f, 320f, 92f);
                window.ShowAuxWindow();
                window.Focus();
            }

            private void OnGUI()
            {
                Event evt = Event.current;
                if ((evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                    || (evt.type == EventType.MouseDown && evt.button == 3))
                {
                    Close();
                    evt.Use();
                    return;
                }

                GUI.SetNextControlName("MontageName");
                newName = EditorGUILayout.TextField("Name", newName);
                EditorGUI.FocusTextInControl("MontageName");

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
                        Close();

                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
                    {
                        if (GUILayout.Button("Rename", GUILayout.Width(80f)))
                            Apply();
                    }
                }

                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Return)
                {
                    Apply();
                    evt.Use();
                }
            }

            private void Apply()
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return;

                onRename?.Invoke(newName.Trim());
                Close();
            }
        }

        private enum MontageBrowserTab
        {
            Libraries,
            Montages
        }
    }
}