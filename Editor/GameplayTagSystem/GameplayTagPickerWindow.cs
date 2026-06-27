using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>인스펙터·드로어에서 열리는 태그 선택 드롭다운 창입니다.</summary>
    internal sealed class GameplayTagPickerWindow : EditorWindow
    {
        private static GameplayTagPickerWindow instance;

        private GameplayTagTreeViewUi treeView;
        private GameplayTagSourceFilePanel sourceFilePanel;
        private ToolbarSearchField searchField;
        private Label descriptionLabel;
        private SerializedProperty boundProperty;
        private SerializedProperty explicitTagsProperty;
        private bool multiSelect;
        private string parentFilter;
        private Action onClosed;

        /// <summary>단일 태그 선택 드롭다운을 엽니다.</summary>
        public static void ShowSingle(Rect activatorRect, SerializedProperty tagNameProperty, string parentFilter, Action onClosed)
        {
            var window = CreateInstance();
            window.multiSelect = false;
            window.boundProperty = tagNameProperty;
            window.explicitTagsProperty = null;
            window.parentFilter = parentFilter;
            window.onClosed = onClosed;
            window.titleContent = new GUIContent(GameplayTagEditorLocalization.PickerSelectTag);
            window.minSize = new Vector2(360, 400);
            ShowDropDown(window, activatorRect, 360f, 440f);
        }

        /// <summary>다중 태그 선택 드롭다운을 엽니다.</summary>
        public static void ShowMulti(Rect activatorRect, SerializedProperty explicitTags, string parentFilter)
        {
            var window = CreateInstance();
            window.multiSelect = true;
            window.boundProperty = null;
            window.explicitTagsProperty = explicitTags;
            window.parentFilter = parentFilter;
            window.onClosed = null;
            window.titleContent = new GUIContent(GameplayTagEditorLocalization.PickerEditTags);
            window.minSize = new Vector2(380, 420);
            ShowDropDown(window, activatorRect, 400f, 480f);
        }

        private static void ShowDropDown(GameplayTagPickerWindow window, Rect activatorRect, float minWidth, float height)
        {
            Rect screenRect = GameplayTagEditorUtility.ToScreenRect(activatorRect);
            float width = Mathf.Max(screenRect.width, minWidth);
            window.ShowAsDropDown(screenRect, new Vector2(width, height));
        }

        private static GameplayTagPickerWindow CreateInstance()
        {
            if (instance != null)
                instance.Close();

            instance = CreateInstance<GameplayTagPickerWindow>();
            return instance;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList(GameplayTagEditorStyles.WindowRootClass);
            GameplayTagEditorStyleSheet.Apply(root);
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            root.style.minHeight = 0;
            root.style.minWidth = 0;
            root.style.overflow = Overflow.Hidden;

            VisualElement toolbar = new();
            toolbar.AddToClassList(GameplayTagEditorStyles.ToolbarClass);
            toolbar.style.flexWrap = Wrap.Wrap;
            root.Add(toolbar);

            searchField = new ToolbarSearchField();
            searchField.AddToClassList(GameplayTagEditorStyles.SearchFieldClass);
            toolbar.Add(searchField);

            if (!string.IsNullOrEmpty(parentFilter))
            {
                Label banner = new(string.Format(GameplayTagEditorLocalization.PickerFilter, parentFilter));
                banner.AddToClassList(GameplayTagEditorStyles.FilterBannerClass);
                banner.style.flexShrink = 0;
                root.Add(banner);
            }

            sourceFilePanel = new GameplayTagSourceFilePanel(showDeleteButton: false);
            sourceFilePanel.SourceFilterChanged += filter =>
            {
                treeView?.SetSourceFileFilter(filter);
                ShowTagDescription(GameplayTag.None);
            };
            sourceFilePanel.style.flexShrink = 0;
            root.Add(sourceFilePanel);

            treeView = new GameplayTagTreeViewUi(
                multiSelect ? GameplayTagTreeSelectionMode.PickerMulti : GameplayTagTreeSelectionMode.PickerSingle);
            treeView.AddToClassList(GameplayTagEditorStyles.TreePaneClass);
            treeView.style.flexGrow = 1;
            treeView.style.flexShrink = 1;
            treeView.style.minHeight = 0;
            treeView.SetParentFilter(parentFilter);
            searchField.RegisterValueChangedCallback(evt => treeView.SetSearch(evt.newValue));
            treeView.TagSelected += OnSingleTagSelected;
            treeView.TagToggled += OnTagToggled;
            treeView.TagInspected += ShowTagDescription;
            root.Add(treeView);

            Label descriptionLabel = new(GameplayTagEditorLocalization.PickerDescriptionPrompt);
            descriptionLabel.AddToClassList(GameplayTagEditorStyles.PickerDescriptionClass);
            descriptionLabel.style.flexShrink = 0;
            root.Add(descriptionLabel);
            this.descriptionLabel = descriptionLabel;

            VisualElement footer = new();
            footer.AddToClassList(GameplayTagEditorStyles.PickerFooterClass);
            root.Add(footer);

            Label hint = new(GameplayTagEditorLocalization.PickerCloseHint);
            hint.AddToClassList(GameplayTagEditorStyles.PickerHintClass);
            footer.Add(hint);

            VisualElement footerActions = new();
            footerActions.AddToClassList(GameplayTagEditorStyles.DetailActionsClass);
            footerActions.style.marginTop = 0;
            footerActions.style.paddingTop = 0;
            footerActions.style.borderTopWidth = 0;
            footer.Add(footerActions);

            if (multiSelect)
            {
                Button clearButton = new(ClearAll) { text = GameplayTagEditorLocalization.PickerClearAll };
                clearButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
                footerActions.Add(clearButton);

                Button doneButton = new(Close) { text = GameplayTagEditorLocalization.Ok };
                doneButton.AddToClassList(GameplayTagEditorStyles.PrimaryButtonClass);
                footerActions.Add(doneButton);
            }
            else
            {
                Button noneButton = new(() => ApplySingleTag(null)) { text = GameplayTagEditorLocalization.PickerNone };
                noneButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
                footerActions.Add(noneButton);
            }

            Button managerButton = new(() => GameplayTagManagerWindow.Open())
            {
                text = GameplayTagEditorLocalization.PickerManager
            };
            managerButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
            footerActions.Add(managerButton);

            treeView.SetSourceFileFilter(sourceFilePanel.GetSelectedFilter());
            RefreshTreeState();

            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<AttachToPanelEvent>(_ => root.Focus());
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.Escape)
                return;

            Close();
            current.Use();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
                return;

            Close();
            evt.StopImmediatePropagation();
        }

        private void RefreshTreeState()
        {
            if (multiSelect)
            {
                if (explicitTagsProperty != null)
                {
                    PruneExplicitTagsProperty();
                    explicitTagsProperty.serializedObject.ApplyModifiedProperties();
                }

                List<string> tags = new();
                if (explicitTagsProperty != null)
                {
                    for (int i = 0; i < explicitTagsProperty.arraySize; i++)
                        tags.Add(explicitTagsProperty.GetArrayElementAtIndex(i).stringValue);
                }

                treeView.SetExplicitTags(tags);
            }
            else if (boundProperty != null)
            {
                treeView.SetSelectedTagName(boundProperty.stringValue);
            }

            treeView.Rebuild();
            ShowInitialDescription();
        }

        private void ShowInitialDescription()
        {
            if (multiSelect)
            {
                if (explicitTagsProperty != null && explicitTagsProperty.arraySize > 0)
                {
                    GameplayTag tag = GameplayTagManager.RequestTag(
                        explicitTagsProperty.GetArrayElementAtIndex(0).stringValue,
                        logWarningIfNotFound: false);
                    ShowTagDescription(tag);
                    return;
                }

                ShowTagDescription(GameplayTag.None);
                return;
            }

            if (boundProperty == null || string.IsNullOrEmpty(boundProperty.stringValue))
            {
                ShowTagDescription(GameplayTag.None);
                return;
            }

            GameplayTag selected = GameplayTagManager.RequestTag(boundProperty.stringValue, logWarningIfNotFound: false);
            ShowTagDescription(selected);
        }

        private void ShowTagDescription(GameplayTag tag)
        {
            if (descriptionLabel == null)
                return;

            if (!tag.IsValid || tag.IsNone)
            {
                descriptionLabel.text = GameplayTagEditorLocalization.PickerDescriptionPrompt;
                return;
            }

            string description = string.IsNullOrEmpty(tag.Description)
                ? GameplayTagEditorLocalization.None
                : tag.Description;

            string source = GameplayTagTreeBuilder.GetSourceLabel(tag);
            descriptionLabel.text = string.IsNullOrEmpty(source)
                ? $"{tag.Name}\n{description}"
                : $"{tag.Name}\n{description}\n[{source}]";
        }

        private void OnSingleTagSelected(GameplayTag tag)
        {
            ApplySingleTag(tag.IsNone ? null : tag.Name);
        }

        private void ApplySingleTag(string tagName)
        {
            if (boundProperty == null)
                return;

            boundProperty.stringValue = tagName;
            boundProperty.serializedObject.ApplyModifiedProperties();
            onClosed?.Invoke();
            Close();
        }

        private void OnTagToggled(GameplayTag tag, bool added)
        {
            if (explicitTagsProperty == null)
                return;

            if (added)
            {
                if (HasExplicitDescendant(tag.Name))
                {
                    RefreshTreeState();
                    return;
                }

                AddExplicitTag(tag.Name);
                RemoveExplicitAncestors(tag);
            }
            else
            {
                RemoveExplicitTag(tag.Name);
            }

            PruneExplicitTagsProperty();
            explicitTagsProperty.serializedObject.ApplyModifiedProperties();
            RefreshTreeState();
        }

        private void RemoveExplicitAncestors(GameplayTag tag)
        {
            foreach (GameplayTag parent in tag.ParentTags)
                RemoveExplicitTag(parent.Name);
        }

        private bool HasExplicitDescendant(string tagName)
        {
            string prefix = tagName + ".";
            for (int i = 0; i < explicitTagsProperty.arraySize; i++)
            {
                string explicitName = explicitTagsProperty.GetArrayElementAtIndex(i).stringValue;
                if (explicitName.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void PruneExplicitTagsProperty()
        {
            HashSet<string> tags = new(StringComparer.Ordinal);
            for (int i = 0; i < explicitTagsProperty.arraySize; i++)
                tags.Add(explicitTagsProperty.GetArrayElementAtIndex(i).stringValue);

            PruneRedundantExplicitTags(tags);

            explicitTagsProperty.arraySize = 0;
            foreach (string tagName in tags)
            {
                explicitTagsProperty.InsertArrayElementAtIndex(explicitTagsProperty.arraySize);
                explicitTagsProperty.GetArrayElementAtIndex(explicitTagsProperty.arraySize - 1).stringValue = tagName;
            }
        }

        private static void PruneRedundantExplicitTags(HashSet<string> tags)
        {
            List<string> toRemove = new();
            foreach (string name in tags)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(name, logWarningIfNotFound: false);
                if (!tag.IsValid)
                    continue;

                foreach (GameplayTag parent in tag.ParentTags)
                {
                    if (tags.Contains(parent.Name))
                        toRemove.Add(parent.Name);
                }
            }

            foreach (string name in toRemove)
                tags.Remove(name);
        }

        private void AddExplicitTag(string tagName)
        {
            for (int i = 0; i < explicitTagsProperty.arraySize; i++)
            {
                if (explicitTagsProperty.GetArrayElementAtIndex(i).stringValue == tagName)
                    return;
            }

            explicitTagsProperty.InsertArrayElementAtIndex(0);
            explicitTagsProperty.GetArrayElementAtIndex(0).stringValue = tagName;
        }

        private void RemoveExplicitTag(string tagName)
        {
            for (int i = 0; i < explicitTagsProperty.arraySize; i++)
            {
                if (explicitTagsProperty.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    explicitTagsProperty.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        private void ClearAll()
        {
            if (explicitTagsProperty == null)
                return;

            explicitTagsProperty.arraySize = 0;
            explicitTagsProperty.serializedObject.ApplyModifiedProperties();
            RefreshTreeState();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
