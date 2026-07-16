using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>게임플레이 태그 계층을 표시하고 선택하는 가상화 트리입니다.</summary>
    internal sealed class GameplayTagTreeViewUI : VisualElement
    {
        private sealed class TagRow : VisualElement
        {
            private readonly GameplayTagTreeViewUI owner;
            private readonly Button foldout;
            private readonly Toggle toggle;
            private readonly Label label;
            private readonly Label sourceLabel;
            private GameplayTagTreeNode node;
            private int rowIndex;
            private bool binding;

            public TagRow(GameplayTagTreeViewUI owner)
            {
                this.owner = owner;
                name = "tag-row";
                AddToClassList(GameplayTagEditorStyles.TreeRowClass);

                foldout = new Button(ToggleExpanded);
                foldout.AddToClassList(GameplayTagEditorStyles.TreeFoldoutClass);
                foldout.RegisterCallback<ClickEvent>(static evt => evt.StopPropagation());
                Add(foldout);

                if (owner.selectionMode == GameplayTagTreeSelectionMode.PickerMulti)
                {
                    toggle = new Toggle();
                    toggle.RegisterValueChangedCallback(OnToggleChanged);
                    Add(toggle);
                }

                label = new Label();
                label.AddToClassList(GameplayTagEditorStyles.TreeLabelClass);
                Add(label);

                sourceLabel = new Label();
                sourceLabel.AddToClassList(GameplayTagEditorStyles.TreeSourceClass);
                Add(sourceLabel);

                RegisterCallback<PointerDownEvent>(OnPointerDown);
                RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            }

            public void Bind(GameplayTagTreeNode value, int index, bool flattenSearch)
            {
                node = value;
                rowIndex = index;
                string tagName = node.Tag.Name;

                style.paddingLeft = 6 + GameplayTagTreeBuilder.GetDepth(node) * 14;
                EnableInClassList(
                    GameplayTagEditorStyles.TreeRowSelectedClass,
                    owner.selectedTagNames.Contains(tagName));

                bool hasFoldout = !flattenSearch && node.Children.Count > 0;
                foldout.style.visibility = hasFoldout ? Visibility.Visible : Visibility.Hidden;
                foldout.text = node.IsExpanded ? "▼" : "▶";

                if (toggle != null)
                {
                    bool isExplicit = owner.explicitTags.Contains(tagName);
                    bool isImplicit = owner.implicitTags.Contains(tagName);
                    binding = true;
                    toggle.showMixedValue = isImplicit && !isExplicit;
                    toggle.SetValueWithoutNotify(isExplicit || isImplicit);
                    toggle.SetEnabled(!owner.HasExplicitDescendant(tagName));
                    binding = false;
                }

                label.text = GameplayTagTreeBuilder.GetRowLabel(node.Tag, flattenSearch);
                label.tooltip = BuildRowTooltip(node.Tag);

                string source = GameplayTagTreeBuilder.GetSourceLabel(node.Tag);
                sourceLabel.text = source;
                sourceLabel.style.display = string.IsNullOrEmpty(source) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            private void ToggleExpanded()
            {
                if (node == null || node.Children.Count == 0)
                    return;

                node.IsExpanded = !node.IsExpanded;
                owner.expandedByRuntimeIndex[node.Tag.RuntimeIndex] = node.IsExpanded;
                owner.Rebuild();
            }

            private void OnToggleChanged(ChangeEvent<bool> evt)
            {
                if (binding || node == null)
                    return;

                owner.NotifyTagInspected(node.Tag);
                owner.TagToggled?.Invoke(node.Tag, evt.newValue);
                toggle.schedule.Execute(() => toggle.panel?.focusController?.focusedElement?.Blur());
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (node == null)
                    return;

                if (owner.selectionMode != GameplayTagTreeSelectionMode.Manager)
                    owner.NotifyTagInspected(node.Tag);

                if (evt.button != 0)
                    return;

                if (owner.selectionMode == GameplayTagTreeSelectionMode.Manager)
                    owner.HandleManagerPointerDown(evt, rowIndex, node.Tag);
                else if (owner.selectionMode == GameplayTagTreeSelectionMode.PickerSingle)
                    owner.HandlePickerSingleClick(node.Tag);
                else
                    return;

                evt.StopPropagation();
            }

            private void OnMouseEnter(MouseEnterEvent _)
            {
                if (node == null)
                    return;

                if (owner.selectionMode != GameplayTagTreeSelectionMode.Manager)
                    owner.NotifyTagInspected(node.Tag);

                if (owner.isDragSelecting && owner.selectionMode == GameplayTagTreeSelectionMode.Manager)
                    owner.ApplyDragSelection(owner.dragAnchorRowIndex, rowIndex);
            }
        }

        public event Action<GameplayTag> TagSelected;
        public event Action<GameplayTag> TagInspected;
        public event Action<IReadOnlyList<GameplayTag>> SelectionChanged;
        public event Action<GameplayTag, bool> TagToggled;

        private readonly ListView listView;
        private readonly Label emptyLabel;
        private readonly List<GameplayTagTreeNode> visibleRows = new();
        private readonly List<GameplayTagTreeNode> roots = new();
        private readonly List<string> namesToRemove = new();
        private readonly List<GameplayTag> selectedTags = new();
        private readonly GameplayTagTreeSelectionMode selectionMode;
        private readonly HashSet<string> explicitTags = new(StringComparer.Ordinal);
        private readonly HashSet<string> implicitTags = new(StringComparer.Ordinal);
        private readonly HashSet<string> selectedTagNames = new(StringComparer.Ordinal);
        private readonly Dictionary<int, bool> expandedByRuntimeIndex = new();

        private string parentFilter;
        private string sourceFileFilter;
        private string search = string.Empty;
        private GameplayTag primaryTag;
        private int lastClickedRowIndex = -1;
        private bool isDragSelecting;
        private int dragAnchorRowIndex = -1;
        private bool hasBuiltTree;
        private bool flattenSearch;
        private bool treeDataDirty = true;
        private int builtGeneration;
        private string builtParentFilter;
        private string builtSourceFileFilter;
        private string builtSearch;
        private IVisualElementScheduledItem pendingSearch;

        public IReadOnlyCollection<string> SelectedTagNames => selectedTagNames;

        public GameplayTagTreeViewUI(GameplayTagTreeSelectionMode mode)
        {
            selectionMode = mode;
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.minHeight = 0;
            style.minWidth = 0;
            style.overflow = Overflow.Hidden;

            listView = new ListView
            {
                itemsSource = visibleRows,
                fixedItemHeight = 26,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                reorderable = false,
                showBorder = false,
                makeItem = () => new TagRow(this),
                bindItem = (element, index) => ((TagRow)element).Bind(visibleRows[index], index, flattenSearch)
            };
            listView.style.flexGrow = 1;
            listView.style.flexShrink = 1;
            listView.style.minHeight = 0;
            listView.style.minWidth = 0;
            Add(listView);

            emptyLabel = new Label { pickingMode = PickingMode.Ignore };
            emptyLabel.AddToClassList(GameplayTagEditorStyles.EmptyStateClass);
            emptyLabel.style.display = DisplayStyle.None;
            Add(emptyLabel);

            RegisterCallback<MouseUpEvent>(OnDragSelectMouseUp);
        }

        public void SetParentFilter(string parentTagName, bool rebuild = true)
        {
            if (string.Equals(parentFilter, parentTagName, StringComparison.Ordinal))
                return;

            parentFilter = parentTagName;
            treeDataDirty = true;
            if (rebuild)
                Rebuild();
        }

        public void SetSourceFileFilter(string sourceFileName, bool rebuild = true)
        {
            if (string.Equals(sourceFileFilter, sourceFileName, StringComparison.Ordinal))
                return;

            sourceFileFilter = sourceFileName;
            treeDataDirty = true;
            if (rebuild)
                Rebuild();
        }

        public void SetSearch(string value)
        {
            value ??= string.Empty;
            if (string.Equals(search, value, StringComparison.Ordinal))
                return;

            search = value;
            treeDataDirty = true;
            pendingSearch?.Pause();
            pendingSearch = schedule.Execute(() =>
            {
                pendingSearch = null;
                Rebuild();
            }).StartingIn(75);
        }

        public void SetSelectedTagName(string tagName)
        {
            selectedTagNames.Clear();
            if (!string.IsNullOrEmpty(tagName))
                selectedTagNames.Add(tagName);

            primaryTag = string.IsNullOrEmpty(tagName)
                ? GameplayTag.None
                : GameplayTagManager.RequestTag(tagName, logWarningIfNotFound: false);

            if (hasBuiltTree)
                RefreshSelectionVisuals();
            else
                Rebuild();

            NotifySelectionChanged();
        }

        public void ClearSelection()
        {
            selectedTagNames.Clear();
            primaryTag = GameplayTag.None;
            lastClickedRowIndex = -1;
            RefreshSelectionVisuals();
            NotifySelectionChanged();
        }

        public void SelectAllVisible()
        {
            if (selectionMode != GameplayTagTreeSelectionMode.Manager || visibleRows.Count == 0)
                return;

            selectedTagNames.Clear();
            for (int i = 0; i < visibleRows.Count; i++)
                selectedTagNames.Add(visibleRows[i].Tag.Name);

            lastClickedRowIndex = visibleRows.Count - 1;
            primaryTag = visibleRows[lastClickedRowIndex].Tag;
            RefreshSelectionVisuals();
            NotifySelectionChanged();
        }

        public void SetExplicitTags(IEnumerable<string> tagNames)
        {
            explicitTags.Clear();
            implicitTags.Clear();

            if (tagNames != null)
            {
                foreach (string tagName in tagNames)
                {
                    if (!string.IsNullOrEmpty(tagName))
                        explicitTags.Add(tagName);
                }
            }

            PruneRedundantExplicitTags();
            foreach (string tagName in explicitTags)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(tagName, logWarningIfNotFound: false);
                if (!tag.IsValid)
                    continue;

                foreach (GameplayTag parent in tag.ParentTags)
                    implicitTags.Add(parent.Name);
            }
            if (hasBuiltTree)
                listView.RefreshItems();
            else
                Rebuild();
        }

        public void Rebuild()
        {
            pendingSearch?.Pause();
            pendingSearch = null;

            int generation = GameplayTagManager.Generation;
            bool rebuildData = treeDataDirty ||
                               !hasBuiltTree ||
                               builtGeneration != generation ||
                               !string.Equals(builtParentFilter, parentFilter, StringComparison.Ordinal) ||
                               !string.Equals(builtSourceFileFilter, sourceFileFilter, StringComparison.Ordinal) ||
                               !string.Equals(builtSearch, search, StringComparison.Ordinal);

            if (rebuildData)
            {
                GameplayTagTreeBuilder.BuildRoots(roots, parentFilter, search, sourceFileFilter);
                ApplyExpandedState(roots);
                builtGeneration = generation;
                builtParentFilter = parentFilter;
                builtSourceFileFilter = sourceFileFilter;
                builtSearch = search;
                treeDataDirty = false;
            }

            flattenSearch = !string.IsNullOrEmpty(search);
            GameplayTagTreeBuilder.CollectVisibleRows(roots, visibleRows, flattenSearch);
            hasBuiltTree = true;

            bool hasRows = visibleRows.Count > 0;
            listView.style.display = hasRows ? DisplayStyle.Flex : DisplayStyle.None;
            emptyLabel.style.display = hasRows ? DisplayStyle.None : DisplayStyle.Flex;
            if (!hasRows)
                emptyLabel.text = GetEmptyTreeMessage();

            listView.Rebuild();
        }

        private void PruneRedundantExplicitTags()
        {
            namesToRemove.Clear();
            foreach (string name in explicitTags)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(name, logWarningIfNotFound: false);
                if (!tag.IsValid)
                    continue;

                foreach (GameplayTag parent in tag.ParentTags)
                {
                    if (explicitTags.Contains(parent.Name))
                        namesToRemove.Add(parent.Name);
                }
            }

            for (int i = 0; i < namesToRemove.Count; i++)
                explicitTags.Remove(namesToRemove[i]);
        }

        private bool HasExplicitDescendant(string tagName)
        {
            string prefix = tagName + ".";
            foreach (string explicitName in explicitTags)
            {
                if (explicitName.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void ApplyExpandedState(IReadOnlyList<GameplayTagTreeNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                GameplayTagTreeNode node = nodes[i];
                if (expandedByRuntimeIndex.TryGetValue(node.Tag.RuntimeIndex, out bool expanded))
                    node.IsExpanded = expanded;

                ApplyExpandedState(node.Children);
            }
        }

        private string GetEmptyTreeMessage()
        {
            if (!GameplayTagEditorUtility.HasSourceFiles())
                return GameplayTagEditorLocalization.NoSourceFilesPrompt;
            if (!string.IsNullOrEmpty(sourceFileFilter))
                return GameplayTagEditorLocalization.NoTagsInSourceFile;
            return GameplayTagEditorLocalization.NoTagsFound;
        }

        private void RefreshSelectionVisuals()
        {
            if (hasBuiltTree)
                listView.RefreshItems();
        }

        private static string BuildRowTooltip(GameplayTag tag)
        {
            string source = GameplayTagTreeBuilder.GetSourceLabel(tag);
            if (string.IsNullOrEmpty(tag.Description))
                return string.IsNullOrEmpty(source) ? tag.Name : $"{tag.Name}\n{source}";

            return string.IsNullOrEmpty(source)
                ? $"{tag.Name}\n{tag.Description}"
                : $"{tag.Name}\n{tag.Description}\n{source}";
        }

        private void HandleManagerPointerDown(PointerDownEvent evt, int rowIndex, GameplayTag tag)
        {
            if (evt.shiftKey && lastClickedRowIndex >= 0)
            {
                if (!evt.ctrlKey)
                    selectedTagNames.Clear();
                SelectRowRange(lastClickedRowIndex, rowIndex);
            }
            else if (evt.ctrlKey)
            {
                ToggleTagSelection(tag);
                lastClickedRowIndex = rowIndex;
            }
            else
            {
                selectedTagNames.Clear();
                selectedTagNames.Add(tag.Name);
                primaryTag = tag;
                lastClickedRowIndex = rowIndex;
                isDragSelecting = true;
                dragAnchorRowIndex = rowIndex;
            }

            RefreshSelectionVisuals();
            NotifySelectionChanged();
        }

        private void HandlePickerSingleClick(GameplayTag tag)
        {
            NotifyTagInspected(tag);
            selectedTagNames.Clear();
            selectedTagNames.Add(tag.Name);
            primaryTag = tag;
            RefreshSelectionVisuals();
            TagSelected?.Invoke(tag);
        }

        private void NotifyTagInspected(GameplayTag tag)
        {
            TagInspected?.Invoke(tag);
        }

        private void OnDragSelectMouseUp(MouseUpEvent _)
        {
            isDragSelecting = false;
        }

        private void ToggleTagSelection(GameplayTag tag)
        {
            if (!selectedTagNames.Add(tag.Name))
                selectedTagNames.Remove(tag.Name);

            primaryTag = selectedTagNames.Contains(tag.Name) ? tag : GameplayTag.None;
        }

        private void SelectRowRange(int fromIndex, int toIndex)
        {
            int start = Math.Min(fromIndex, toIndex);
            int end = Math.Max(fromIndex, toIndex);
            for (int i = start; i <= end; i++)
                selectedTagNames.Add(visibleRows[i].Tag.Name);

            primaryTag = visibleRows[toIndex].Tag;
            lastClickedRowIndex = toIndex;
        }

        private void ApplyDragSelection(int anchorIndex, int currentIndex)
        {
            if ((uint)anchorIndex >= (uint)visibleRows.Count || (uint)currentIndex >= (uint)visibleRows.Count)
                return;

            selectedTagNames.Clear();
            SelectRowRange(anchorIndex, currentIndex);
            RefreshSelectionVisuals();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            selectedTags.Clear();
            foreach (string tagName in selectedTagNames)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(tagName, logWarningIfNotFound: false);
                if (tag.IsValid)
                    selectedTags.Add(tag);
            }

            SelectionChanged?.Invoke(selectedTags);
        }
    }
}