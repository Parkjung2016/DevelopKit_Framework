using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>게임플레이 태그 JSON 관리 에디터 창입니다.</summary>
    public sealed class GameplayTagManagerWindow : EditorWindow
    {
        private GameplayTagSplitView splitView;
        private GameplayTagTreeViewUI treeView;
        private ScrollView detailScroll;
        private VisualElement detailHost;
        private ToolbarSearchField searchField;
        private GameplayTagSourceFilePanel sourceFilePanel;
        private GameplayTagAddTagPanel addTagForm;
        private List<GameplayTag> selectedTags = new();
        private List<string> lastDisplayedTagNames = new();

        /// <summary>태그 관리 에디터 창을 엽니다.</summary>
        [MenuItem("PJDev/Gameplay Tags/Tag Manager")]
        public static void Open()
        {
            var window = GetWindow<GameplayTagManagerWindow>();
            window.titleContent = new GUIContent(GameplayTagEditorLocalization.WindowTitle);
            window.minSize = new Vector2(480, 360);
            window.Show();
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

            BuildToolbar(root);

            splitView = new GameplayTagSplitView(320f);
            splitView.style.flexGrow = 1;
            splitView.style.flexShrink = 1;
            splitView.style.minHeight = 0;
            splitView.style.minWidth = 0;
            root.Add(splitView);

            sourceFilePanel = new GameplayTagSourceFilePanel();
            sourceFilePanel.SourceFilterChanged += filter =>
            {
                CloseAddForm();
                treeView.SetSourceFileFilter(filter);
                treeView.ClearSelection();
                selectedTags.Clear();
                lastDisplayedTagNames.Clear();
                ShowDetail(null);
            };
            sourceFilePanel.SourceFilesChanged += () =>
            {
                CloseAddForm();
                treeView.ClearSelection();
                selectedTags.Clear();
                lastDisplayedTagNames.Clear();
                RebuildTree();
                ShowDetail(null);
            };
            splitView.LeftPane.Add(sourceFilePanel);

            treeView = new GameplayTagTreeViewUI(GameplayTagTreeSelectionMode.Manager);
            treeView.AddToClassList(GameplayTagEditorStyles.TreePaneClass);
            treeView.SelectionChanged += tags =>
            {
                selectedTags = new List<GameplayTag>(tags);
                ShowDetail(selectedTags);
            };
            splitView.LeftPane.Add(treeView);
            treeView.SetSourceFileFilter(sourceFilePanel.GetSelectedFilter());

            detailScroll = new ScrollView(ScrollViewMode.Vertical);
            detailScroll.style.flexGrow = 1;
            detailScroll.style.flexShrink = 1;
            detailScroll.style.minHeight = 0;
            detailScroll.style.minWidth = 0;
            splitView.RightPane.Add(detailScroll);

            detailHost = new VisualElement();
            detailHost.AddToClassList(GameplayTagEditorStyles.DetailPanelClass);
            detailHost.style.flexGrow = 1;
            detailScroll.Add(detailHost);

            RebuildTree();
            ShowDetail(null);

            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnRootKeyDown);
            root.RegisterCallback<AttachToPanelEvent>(_ => root.Focus());
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (addTagForm != null)
                return;

            if (IsTextInputFocused())
                return;

            if (evt.keyCode == KeyCode.A && (evt.ctrlKey || evt.commandKey))
            {
                treeView.SelectAllVisible();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode != KeyCode.Delete)
                return;

            if (selectedTags.Count == 0)
                return;

            TryDeleteSelectedTags(evt.shiftKey
                ? GameplayTagDeleteMode.Hierarchy
                : GameplayTagDeleteMode.TagOnly);

            evt.StopPropagation();
        }

        private bool IsTextInputFocused()
        {
            VisualElement focused = rootVisualElement.panel?.focusController?.focusedElement as VisualElement;
            if (focused == null)
                return false;

            for (VisualElement current = focused; current != null; current = current.parent)
            {
                if (current is TextInputBaseField<string>)
                    return true;
            }

            return false;
        }

        private void BuildToolbar(VisualElement root)
        {
            VisualElement toolbar = new();
            toolbar.AddToClassList(GameplayTagEditorStyles.ToolbarClass);
            toolbar.style.flexWrap = Wrap.Wrap;
            root.Add(toolbar);

            searchField = new ToolbarSearchField();
            searchField.AddToClassList(GameplayTagEditorStyles.SearchFieldClass);
            searchField.RegisterValueChangedCallback(evt => treeView.SetSearch(evt.newValue));
            toolbar.Add(searchField);

            Button refreshButton = new(RebuildAll) { text = GameplayTagEditorLocalization.Refresh };
            refreshButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
            toolbar.Add(refreshButton);

            Button addButton = new(() => ShowAddForm(null)) { text = GameplayTagEditorLocalization.AddTag };
            addButton.AddToClassList(GameplayTagEditorStyles.PrimaryButtonClass);
            toolbar.Add(addButton);

            Button deleteButton = new(() => TryDeleteSelectedTags(GameplayTagDeleteMode.TagOnly))
                { text = GameplayTagEditorLocalization.DeleteSelected };
            deleteButton.tooltip = GameplayTagEditorLocalization.DeleteSelectedTooltip;
            deleteButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
            toolbar.Add(deleteButton);

            Button openFolderButton = new(GameplayTagEditorUtility.OpenTagsDirectory)
            {
                text = GameplayTagEditorLocalization.OpenFolder
            };
            openFolderButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
            toolbar.Add(openFolderButton);

            Button generateScriptsButton = new(() => GameplayTagScriptGenerator.GenerateFromMenu())
            {
                text = GameplayTagEditorLocalization.GenerateTagScripts,
                tooltip = GameplayTagEditorLocalization.GenerateTagScriptsTooltip
            };
            generateScriptsButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
            toolbar.Add(generateScriptsButton);
        }

        private void RebuildAll()
        {
            sourceFilePanel?.Refresh();
            RebuildTree();
        }

        private void ShowAddForm(string defaultParentTagName)
        {
            CloseAddForm();

            string parent = defaultParentTagName;
            string sourceFile = sourceFilePanel?.GetSelectedFilter();

            if (!string.IsNullOrEmpty(parent))
            {
                GameplayTag parentTag = GameplayTagManager.RequestTag(parent, logWarningIfNotFound: false);
                if (parentTag.IsValid && string.IsNullOrEmpty(sourceFile))
                    sourceFile = GameplayTagSourceUtility.GetPrimaryFileSourceName(parentTag);
            }
            else if (selectedTags.Count == 1)
            {
                parent = selectedTags[0].Name;
                if (string.IsNullOrEmpty(sourceFile))
                    sourceFile = GameplayTagSourceUtility.GetPrimaryFileSourceName(selectedTags[0]);
            }

            addTagForm = new GameplayTagAddTagPanel(parent, sourceFile);
            addTagForm.style.flexShrink = 1;
            addTagForm.style.maxHeight = 280;
            addTagForm.style.minHeight = 0;
            addTagForm.style.overflow = Overflow.Hidden;
            addTagForm.SetDefaultParent(parent);
            addTagForm.TagCreated += tag =>
            {
                CloseAddForm();
                RebuildAll();
                treeView.SetSelectedTagName(tag.Name);
            };
            addTagForm.Cancelled += CloseAddForm;

            rootVisualElement.Insert(1, addTagForm);
            addTagForm.FocusNameField();
        }

        private void CloseAddForm()
        {
            if (addTagForm == null)
                return;

            addTagForm.RemoveFromHierarchy();
            addTagForm = null;
        }

        private void RebuildTree()
        {
            treeView?.Rebuild();
            if (selectedTags.Count == 1)
                ShowDetail(selectedTags);
        }

        private void ShowDetail(IReadOnlyList<GameplayTag> tags)
        {
            List<string> nextNames = new();
            if (tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                    nextNames.Add(tags[i].Name);
            }

            bool isEmpty = tags == null || tags.Count == 0;
            if (!isEmpty && TagNamesEqual(nextNames, lastDisplayedTagNames))
                return;

            lastDisplayedTagNames = nextNames;
            detailHost.Clear();

            Label title = new(GameplayTagEditorLocalization.TagDetails);
            title.AddToClassList(GameplayTagEditorStyles.PanelTitleClass);
            detailHost.Add(title);
            GameplayTag tag;
            VisualElement actions = new();

            if (tags == null || tags.Count == 0)
            {
                Label empty = new(GameplayTagEditorLocalization.SelectTagPrompt);
                empty.AddToClassList(GameplayTagEditorStyles.EmptyStateClass);
                detailHost.Add(empty);
                return;
            }

            if (tags.Count > 1)
            {
                Label multi = new(string.Format(GameplayTagEditorLocalization.MultiSelectPrompt, tags.Count));
                multi.AddToClassList(GameplayTagEditorStyles.DetailValueClass);
                multi.style.marginBottom = 10;
                detailHost.Add(multi);

                ScrollView listScroll = new(ScrollViewMode.Vertical);
                listScroll.AddToClassList(GameplayTagEditorStyles.DetailMultiListClass);
                for (int i = 0; i < tags.Count; i++)
                {
                    tag = tags[i];
                    Label item = new(tag.Name);
                    item.AddToClassList(GameplayTagEditorStyles.DetailMultiItemClass);
                    listScroll.Add(item);
                }

                detailHost.Add(listScroll);

                if (HasAnyDeletable(tags))
                {
                    bool hasDescendants = false;
                    foreach (GameplayTag selected in tags)
                    {
                        if (HasDescendantsInSameFile(selected))
                        {
                            hasDescendants = true;
                            break;
                        }
                    }

                    actions.AddToClassList(GameplayTagEditorStyles.DetailActionsClass);

                    Button deleteButton = new(() => TryDeleteSelectedTags(GameplayTagDeleteMode.TagOnly))
                    {
                        text = GameplayTagEditorLocalization.DeleteSelected
                    };
                    deleteButton.tooltip = GameplayTagEditorLocalization.DeleteSelectedTooltip;
                    deleteButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
                    actions.Add(deleteButton);

                    if (hasDescendants)
                    {
                        Button deleteHierarchyButton = new(() => TryDeleteSelectedTags(GameplayTagDeleteMode.Hierarchy))
                        {
                            text = GameplayTagEditorLocalization.DeleteHierarchy
                        };
                        deleteHierarchyButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
                        actions.Add(deleteHierarchyButton);
                    }

                    detailHost.Add(actions);
                }

                return;
            }

            tag = tags[0];

            if (GameplayTagTreeBuilder.CanEdit(tag))
            {
                AddEditableTagDetail(tag);
            }
            else
            {
                AddDetailField(GameplayTagEditorLocalization.Name, tag.Name);
                AddDetailField(
                    GameplayTagEditorLocalization.Description,
                    string.IsNullOrEmpty(tag.Description) ? GameplayTagEditorLocalization.None : tag.Description);
                Label readOnlyHint = new(GameplayTagEditorLocalization.TagEditReadOnly);
                readOnlyHint.AddToClassList(GameplayTagEditorStyles.DetailLabelClass);
                readOnlyHint.style.marginBottom = 8;
                detailHost.Add(readOnlyHint);
            }

            AddDetailField(GameplayTagEditorLocalization.NameLabel, tag.Label);
            AddDetailField(
                GameplayTagEditorLocalization.Parent,
                tag.ParentTag.IsNone ? GameplayTagEditorLocalization.Root : tag.ParentTag.Name);
            AddDetailField(
                GameplayTagEditorLocalization.Children,
                tag.IsLeaf ? GameplayTagEditorLocalization.Leaf : string.Join(", ", GetChildNames(tag)));
            AddDetailField(GameplayTagEditorLocalization.Source, GameplayTagTreeBuilder.GetSourceLabel(tag));

            actions = new();
            actions.AddToClassList(GameplayTagEditorStyles.DetailActionsClass);

            Button addChildButton = new(() => ShowAddForm(tag.Name))
            {
                text = GameplayTagEditorLocalization.AddChild
            };
            addChildButton.AddToClassList(GameplayTagEditorStyles.PrimaryButtonClass);
            actions.Add(addChildButton);

            if (GameplayTagTreeBuilder.CanDelete(tag))
            {
                if (HasDescendantsInSameFile(tag))
                {
                    Button deleteHierarchyButton = new(() => TryDeleteSelectedTags(GameplayTagDeleteMode.Hierarchy))
                    {
                        text = GameplayTagEditorLocalization.DeleteHierarchy
                    };
                    deleteHierarchyButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
                    actions.Add(deleteHierarchyButton);

                    Button deleteOnlyButton = new(() => TryDeleteSelectedTags(GameplayTagDeleteMode.TagOnly))
                    {
                        text = GameplayTagEditorLocalization.Delete
                    };
                    deleteOnlyButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
                    actions.Add(deleteOnlyButton);
                }
                else
                {
                    Button deleteButton = new(() => TryDeleteSelectedTags(GameplayTagDeleteMode.TagOnly))
                    {
                        text = GameplayTagEditorLocalization.Delete
                    };
                    deleteButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
                    actions.Add(deleteButton);
                }
            }

            detailHost.Add(actions);
        }

        private static bool TagNamesEqual(List<string> a, List<string> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private void AddEditableTagDetail(GameplayTag tag)
        {
            string sourceFile = GameplayTagSourceUtility.GetPrimaryFileSourceName(tag);
            GameplayTagNameComposer.SplitTagName(tag.Name, out string initialParent, out string initialSegment);

            List<string> parentOptions = GameplayTagNameComposer.BuildParentOptionsForSource(sourceFile, tag.Name);
            int parentIndex = parentOptions.FindIndex(p =>
                string.Equals(p, initialParent, StringComparison.OrdinalIgnoreCase));
            if (parentIndex < 0 && initialParent != GameplayTagNameComposer.RootParentLabel)
            {
                parentOptions.Insert(1, initialParent);
                parentIndex = 1;
            }
            else if (parentIndex < 0)
            {
                parentIndex = 0;
            }

            VisualElement section = new();
            section.AddToClassList(GameplayTagEditorStyles.DetailSectionClass);

            PopupField<string> parentField = new(
                GameplayTagEditorLocalization.Parent,
                parentOptions,
                parentIndex);
            parentField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            section.Add(parentField);

            TextField segmentField = new(GameplayTagEditorLocalization.Name) { value = initialSegment };
            segmentField.tooltip = GameplayTagEditorLocalization.NameTooltip;
            segmentField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            section.Add(segmentField);

            Label previewLabel = new();
            previewLabel.AddToClassList(GameplayTagEditorStyles.DetailLabelClass);
            previewLabel.style.whiteSpace = WhiteSpace.Normal;
            previewLabel.style.marginBottom = 6;
            section.Add(previewLabel);

            void UpdatePreview() =>
                previewLabel.text = GameplayTagNameComposer.BuildEditPreviewText(
                    parentField.value,
                    segmentField.value,
                    sourceFile);

            parentField.RegisterValueChangedCallback(_ => UpdatePreview());
            segmentField.RegisterValueChangedCallback(_ => UpdatePreview());
            UpdatePreview();

            Label descriptionLabel = new(GameplayTagEditorLocalization.Description);
            descriptionLabel.AddToClassList(GameplayTagEditorStyles.DetailLabelClass);
            section.Add(descriptionLabel);

            TextField descriptionField = new()
            {
                value = string.IsNullOrEmpty(tag.Description) ? string.Empty : tag.Description
            };
            descriptionField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            descriptionField.multiline = true;
            descriptionField.style.minHeight = 48;
            section.Add(descriptionField);

            Label editErrorLabel = new();
            editErrorLabel.AddToClassList(GameplayTagEditorStyles.ValidationErrorClass);
            editErrorLabel.style.display = DisplayStyle.None;
            section.Add(editErrorLabel);

            Button saveButton = new(() => TrySaveTagEdits(
                tag,
                parentField.value,
                segmentField.value,
                descriptionField.value,
                sourceFile,
                editErrorLabel))
            {
                text = GameplayTagEditorLocalization.Save
            };
            saveButton.AddToClassList(GameplayTagEditorStyles.PrimaryButtonClass);
            saveButton.style.marginTop = 6;
            section.Add(saveButton);

            detailHost.Add(section);
        }

        private void TrySaveTagEdits(
            GameplayTag tag,
            string parentTagName,
            string segmentName,
            string newComment,
            string sourceFile,
            Label errorLabel)
        {
            errorLabel.style.display = DisplayStyle.None;

            newComment = newComment?.Trim() ?? string.Empty;

            if (GameplayTagNameComposer.IsInvalidParentForRename(tag.Name, parentTagName))
            {
                errorLabel.text = GameplayTagEditorLocalization.TagEditInvalidParent;
                errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            if (!GameplayTagNameComposer.TryComposeValidName(
                    parentTagName,
                    segmentName,
                    out string newName,
                    out string composeError))
            {
                errorLabel.text = composeError;
                errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            bool rename = !string.Equals(newName, tag.Name, StringComparison.Ordinal);
            string originalName = tag.Name;

            FileGameplayTagSource targetSource = null;
            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is FileGameplayTagSource fileSource)
                {
                    targetSource = fileSource;
                    break;
                }
            }

            if (targetSource == null)
            {
                errorLabel.text = GameplayTagEditorLocalization.TagEditReadOnly;
                errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            if (rename)
            {
                if (!GameplayTagCrossFileUtility.TryResolveRenameConflictInOtherFile(
                        sourceFile,
                        newName,
                        out string renameConflictError))
                {
                    if (!string.IsNullOrEmpty(renameConflictError))
                    {
                        errorLabel.text = renameConflictError;
                        errorLabel.style.display = DisplayStyle.Flex;
                    }

                    return;
                }

                List<string> crossFileTags =
                    GameplayTagCrossFileUtility.CollectCrossFileTagNames(sourceFile, newName);

                if (!GameplayTagCrossFileUtility.TryResolveTagsInOtherFilesForMove(
                        targetSource,
                        crossFileTags,
                        out string moveError))
                {
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        errorLabel.text = moveError;
                        errorLabel.style.display = DisplayStyle.Flex;
                    }

                    return;
                }
            }

            List<string> missingParents = rename
                ? GameplayTagNameComposer.GetMissingParentsInSourceFile(newName, sourceFile)
                : new List<string>();

            if (missingParents.Count > 0)
            {
                string message = string.Format(
                    GameplayTagEditorLocalization.CreateMissingParentsMessage,
                    sourceFile,
                    string.Join("\n", missingParents));

                if (!EditorUtility.DisplayDialog(
                        GameplayTagEditorLocalization.CreateMissingParentsTitle,
                        message,
                        GameplayTagEditorLocalization.Ok,
                        GameplayTagEditorLocalization.Cancel))
                {
                    return;
                }
            }

            bool createMissingParents = missingParents.Count > 0;
            bool applied = false;

            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is not IGameplayTagEditHandler handler)
                    continue;

                if (rename)
                {
                    if (!handler.TryRenameTag(
                            originalName,
                            newName,
                            out string renameError,
                            createMissingParents))
                    {
                        if (IsTagNotInFileError(renameError))
                            continue;

                        errorLabel.text = GameplayTagEditorUtility.LocalizeRuntimeMessage(renameError);
                        errorLabel.style.display = DisplayStyle.Flex;
                        return;
                    }

                    applied = true;
                }

                if (!handler.TryUpdateComment(newName, newComment, out string commentError))
                {
                    if (IsTagNotInFileError(commentError))
                        continue;

                    errorLabel.text = GameplayTagEditorUtility.LocalizeRuntimeMessage(commentError);
                    errorLabel.style.display = DisplayStyle.Flex;
                    return;
                }

                applied = true;
            }

            if (!applied)
            {
                errorLabel.text = GameplayTagEditorLocalization.TagEditReadOnly;
                errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            GameplayTagManager.ReloadTags();
            GameplayTag updated = GameplayTagManager.RequestTag(newName, logWarningIfNotFound: false);
            if (!updated.IsValid)
            {
                errorLabel.text = GameplayTagEditorLocalization.TagCreateReloadFailed;
                errorLabel.style.display = DisplayStyle.Flex;
                return;
            }

            selectedTags = new List<GameplayTag> { updated };
            lastDisplayedTagNames.Clear();
            treeView.SetSelectedTagName(updated.Name);
            RebuildAll();
            ShowDetail(selectedTags);
        }

        private static bool IsTagNotInFileError(string error) =>
            error != null && error.StartsWith("TAG_NOT_IN_FILE:", StringComparison.Ordinal);

        private static IEnumerable<string> GetChildNames(GameplayTag tag)
        {
            for (int i = 0; i < tag.ChildTags.Length; i++)
            {
                var child = tag.ChildTags[i];
                yield return child.Label;
            }
        }

        private void AddDetailField(string label, string value)
        {
            VisualElement section = new();
            section.AddToClassList(GameplayTagEditorStyles.DetailSectionClass);

            Label labelElement = new(label);
            labelElement.AddToClassList(GameplayTagEditorStyles.DetailLabelClass);
            section.Add(labelElement);

            Label valueElement = new(value);
            valueElement.AddToClassList(GameplayTagEditorStyles.DetailValueClass);
            section.Add(valueElement);

            detailHost.Add(section);
        }

        private void TryDeleteSelectedTags(GameplayTagDeleteMode mode)
        {
            List<GameplayTag> tagsToDelete = GetTagsForDelete();
            if (tagsToDelete.Count == 0)
                return;

            string message;
            string title = tagsToDelete.Count == 1
                ? GameplayTagEditorLocalization.DeleteTagTitle
                : GameplayTagEditorLocalization.DeleteTagsTitle;

            if (mode == GameplayTagDeleteMode.Hierarchy)
            {
                if (tagsToDelete.Count == 1)
                {
                    int descendantCount = CountDescendantsInSameFile(tagsToDelete[0]);
                    message = string.Format(
                        GameplayTagEditorLocalization.DeleteTagHierarchyMessage,
                        tagsToDelete[0].Name,
                        descendantCount);
                }
                else
                {
                    message = string.Format(
                        GameplayTagEditorLocalization.DeleteTagsHierarchyMessage,
                        tagsToDelete.Count);
                }
            }
            else if (tagsToDelete.Count == 1)
            {
                message = HasDescendantsInSameFile(tagsToDelete[0])
                    ? string.Format(GameplayTagEditorLocalization.DeleteTagPromoteMessage, tagsToDelete[0].Name)
                    : string.Format(GameplayTagEditorLocalization.DeleteTagMessage, tagsToDelete[0].Name);
            }
            else
            {
                bool hasDescendants = false;
                foreach (GameplayTag tag in tagsToDelete)
                {
                    if (HasDescendantsInSameFile(tag))
                    {
                        hasDescendants = true;
                        break;
                    }
                }

                message = hasDescendants
                    ? string.Format(GameplayTagEditorLocalization.DeleteTagsPromoteMessage, tagsToDelete.Count)
                    : string.Format(GameplayTagEditorLocalization.DeleteTagsMessage, tagsToDelete.Count);
            }

            if (!EditorUtility.DisplayDialog(title, message, GameplayTagEditorLocalization.Delete,
                    GameplayTagEditorLocalization.Cancel))
                return;

            CloseAddForm();

            List<GameplayTag> ordered = new(tagsToDelete);
            ordered.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));

            foreach (GameplayTag tag in ordered)
            {
                if (!GameplayTagTreeBuilder.CanDelete(tag))
                    continue;

                for (int i = 0; i < tag.Definition.SourceCount; i++)
                {
                    if (tag.Definition.GetSource(i) is not IDeleteTagHandler handler)
                        continue;

                    if (!handler.TryValidateDelete(tag.Name, mode, out string validateError))
                    {
                        EditorUtility.DisplayDialog(
                            title,
                            GameplayTagEditorUtility.LocalizeRuntimeMessage(validateError),
                            GameplayTagEditorLocalization.Ok);
                        return;
                    }
                }
            }

            foreach (GameplayTag tag in ordered)
            {
                if (!GameplayTagTreeBuilder.CanDelete(tag))
                    continue;

                for (int i = 0; i < tag.Definition.SourceCount; i++)
                {
                    if (tag.Definition.GetSource(i) is not IDeleteTagHandler handler)
                        continue;

                    if (handler.TryDeleteTag(tag.Name, mode, out string deleteError))
                        continue;

                    EditorUtility.DisplayDialog(
                        title,
                        GameplayTagEditorUtility.LocalizeRuntimeMessage(deleteError),
                        GameplayTagEditorLocalization.Ok);
                    return;
                }
            }

            GameplayTagManager.ReloadTags();
            selectedTags.Clear();
            lastDisplayedTagNames.Clear();
            RebuildAll();
            treeView.ClearSelection();
            ShowDetail(null);
        }

        private List<GameplayTag> GetTagsForDelete()
        {
            if (selectedTags.Count > 0)
                return new List<GameplayTag>(selectedTags);

            List<GameplayTag> tags = new();
            foreach (string name in lastDisplayedTagNames)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(name, logWarningIfNotFound: false);
                if (tag.IsValid)
                    tags.Add(tag);
            }

            return tags;
        }

        private void ClearSelectionAndDetail()
        {
            selectedTags.Clear();
            lastDisplayedTagNames.Clear();
            treeView.ClearSelection();
            ShowDetail(null);
        }

        private static bool HasAnyDeletable(IReadOnlyList<GameplayTag> tags)
        {
            foreach (GameplayTag tag in tags)
            {
                if (GameplayTagTreeBuilder.CanDelete(tag))
                    return true;
            }

            return false;
        }

        private static int CountDescendantsInSameFile(GameplayTag tag)
        {
            string prefix = tag.Name + ".";
            int count = 0;

            foreach (GameplayTag candidate in GameplayTagManager.GetAllTags())
            {
                if (!candidate.Name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (GameplayTagSourceUtility.SharesFileSource(tag, candidate))
                    count++;
            }

            return count;
        }

        private static bool HasDescendantsInSameFile(GameplayTag tag)
        {
            string prefix = tag.Name + ".";

            foreach (GameplayTag candidate in GameplayTagManager.GetAllTags())
            {
                if (!candidate.Name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (GameplayTagSourceUtility.SharesFileSource(tag, candidate))
                    return true;
            }

            return false;
        }
    }
}