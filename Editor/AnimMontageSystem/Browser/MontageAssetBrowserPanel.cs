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
                    PopulateAssets<AnimNotifySO>(filter);
                    PopulateAssets<AnimNotifyStateSO>(filter);
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
                    if (asset is AnimMontageSO montage)
                        context.SetMontage(montage);
                    else
                        context.SetSelected(asset);
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

                if (!string.IsNullOrEmpty(filter) &&
                    clip.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                scrollView.Add(CreateRow(clip.name, clip, () => context.SetSelected(clip)));
            }
        }

        private VisualElement CreateRow(string label, UnityEngine.Object asset, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList(AnimMontageEditorStyles.BrowserRowClass);
            row.RegisterCallback<ClickEvent>(_ => onClick());

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

        private enum MontageBrowserTab
        {
            Montages,
            Notifies,
            Clips
        }
    }
}
