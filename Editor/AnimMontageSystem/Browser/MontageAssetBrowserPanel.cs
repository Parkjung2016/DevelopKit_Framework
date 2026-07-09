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
        private readonly ScrollView scrollView = new(ScrollViewMode.Vertical);
        private readonly ToolbarSearchField searchField = new();
        private MontageBrowserTab activeTab = MontageBrowserTab.Montages;

        public MontageAssetBrowserPanel(MontageEditorContext context)
        {
            this.context = context;
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
            tabs.Add(CreateTabButton("Montages", MontageBrowserTab.Montages));
            tabs.Add(CreateTabButton("Notifies", MontageBrowserTab.Notifies));
            tabs.Add(CreateTabButton("Clips", MontageBrowserTab.Clips));
            Add(tabs);

            searchField.style.flexShrink = 0;
            searchField.RegisterValueChangedCallback(_ => Rebuild());
            Add(searchField);

            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.minHeight = 0;
            Add(scrollView);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            context.Changed += Rebuild;
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
            return button;
        }

        private void Rebuild()
        {
            scrollView.Clear();
            string filter = searchField.value?.Trim() ?? string.Empty;

            switch (activeTab)
            {
                case MontageBrowserTab.Montages:
                    PopulateAssets<AnimMontageSO>(filter);
                    break;
                case MontageBrowserTab.Notifies:
                    PopulateNotifyTypes<AnimNotify>(filter, "Notify");
                    PopulateNotifyTypes<AnimNotifyState>(filter, "Notify State");
                    break;
                case MontageBrowserTab.Clips:
                    PopulateClips(filter);
                    break;
            }
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
                    if (asset is AnimMontageSO montage)
                        context.SetMontage(montage);
                    else
                        context.SetSelected(asset);
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

                scrollView.Add(CreateTypeRow($"{category}: {type.Name}", type));
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
                    context.SetSelected(clip);
                }));
            }
        }

        private VisualElement CreateRow(string label, UnityEngine.Object asset, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList(AnimMontageEditorStyles.BrowserRowClass);
            row.RegisterCallback<ClickEvent>(_ => onClick());
            if (asset is AnimMontageSO montage)
            {
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Rename", _ => RenameMontage(montage));
                    evt.menu.AppendAction("Delete", _ => DeleteMontage(montage));
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

        private VisualElement CreateTypeRow(string label, Type type)
        {
            var row = new VisualElement();
            row.AddToClassList(AnimMontageEditorStyles.BrowserRowClass);
            row.RegisterCallback<ClickEvent>(_ => Debug.Log(type.FullName));

            row.Add(new Label(label));
            return row;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (activeTab != MontageBrowserTab.Montages || context.Montage == null)
                return;

            if (evt.keyCode == KeyCode.F2)
            {
                RenameMontage(context.Montage);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Delete)
            {
                DeleteMontage(context.Montage);
                evt.StopPropagation();
            }
        }

        private void RenameMontage(AnimMontageSO montage)
        {
            if (montage == null)
                return;

            RenameAssetPopup.Show(montage, newName =>
            {
                string path = AssetDatabase.GetAssetPath(montage);
                if (string.IsNullOrEmpty(path))
                    return;

                string error = AssetDatabase.RenameAsset(path, newName);
                if (!string.IsNullOrEmpty(error))
                {
                    EditorUtility.DisplayDialog("Rename Montage", error, "OK");
                    return;
                }

                AssetDatabase.SaveAssets();
                context.SetMontage(montage);
                Rebuild();
            });
        }

        private void DeleteMontage(AnimMontageSO montage)
        {
            if (montage == null)
                return;

            string path = AssetDatabase.GetAssetPath(montage);
            if (string.IsNullOrEmpty(path))
                return;

            if (!EditorUtility.DisplayDialog(
                    "Delete Montage",
                    $"Delete '{montage.name}'?\n\n{path}",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            bool deletingCurrent = context.Montage == montage;
            if (!AssetDatabase.DeleteAsset(path))
            {
                EditorUtility.DisplayDialog("Delete Montage", "Failed to delete asset.", "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            if (deletingCurrent)
                context.SetMontage(null);
            else
                Rebuild();
        }

        private sealed class RenameAssetPopup : EditorWindow
        {
            private AnimMontageSO target;
            private string newName;
            private Action<string> onRename;

            public static void Show(AnimMontageSO target, Action<string> onRename)
            {
                if (target == null)
                    return;

                var window = CreateInstance<RenameAssetPopup>();
                window.titleContent = new GUIContent("Rename Montage");
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
            Montages,
            Notifies,
            Clips
        }
    }
}
