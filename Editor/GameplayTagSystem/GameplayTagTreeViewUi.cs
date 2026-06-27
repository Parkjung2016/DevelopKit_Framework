using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>게임플레이 태그 계층을 표시·선택하는 UIElements 트리 뷰입니다.</summary>
    internal sealed class GameplayTagTreeViewUI : VisualElement
    {
        public event Action<GameplayTag> TagSelected;
        public event Action<GameplayTag> TagInspected;
        public event Action<IReadOnlyList<GameplayTag>> SelectionChanged;
        public event Action<GameplayTag, bool> TagToggled;

        private readonly ScrollView scrollView = new(ScrollViewMode.Vertical);
        private readonly VisualElement rowsHost = new();
        private readonly List<GameplayTagTreeNode> visibleRows = new();
        private readonly List<GameplayTagTreeNode> roots = new();
        private readonly Dictionary<int, VisualElement> rowsByRuntimeIndex = new();
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

        public IReadOnlyCollection<string> SelectedTagNames => selectedTagNames;

        public GameplayTagTreeViewUI(GameplayTagTreeSelectionMode mode)
        {
            selectionMode = mode;
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.minHeight = 0;
            style.minWidth = 0;
            style.overflow = Overflow.Hidden;

            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.minHeight = 0;
            scrollView.style.minWidth = 0;
            rowsHost.style.flexGrow = 1;
            scrollView.Add(rowsHost);
            Add(scrollView);

            RegisterCallback<MouseUpEvent>(OnDragSelectMouseUp);
        }

        /// <summary>부모 태그 접두어로 표시할 태그를 제한합니다.</summary>
        public void SetParentFilter(string parentTagName)
        {
            parentFilter = parentTagName;
            Rebuild();
        }

        /// <summary>JSON 소스 파일별로 표시할 태그를 제한합니다.</summary>
        public void SetSourceFileFilter(string sourceFileName)
        {
            sourceFileFilter = sourceFileName;
            Rebuild();
        }

        /// <summary>이름·라벨 검색어로 트리를 필터링합니다.</summary>
        public void SetSearch(string value)
        {
            search = value ?? string.Empty;
            Rebuild();
        }

        /// <summary>단일 태그를 선택 상태로 설정합니다.</summary>
        public void SetSelectedTagName(string tagName)
        {
            selectedTagNames.Clear();
            if (!string.IsNullOrEmpty(tagName))
                selectedTagNames.Add(tagName);

            primaryTag = string.IsNullOrEmpty(tagName)
                ? GameplayTag.None
                : GameplayTagManager.RequestTag(tagName);

            if (rowsByRuntimeIndex.Count > 0)
                RefreshSelectionVisuals();
            else
                Rebuild();

            NotifySelectionChanged();
        }

        /// <summary>현재 선택을 모두 해제합니다.</summary>
        public void ClearSelection()
        {
            selectedTagNames.Clear();
            primaryTag = GameplayTag.None;
            lastClickedRowIndex = -1;

            if (rowsByRuntimeIndex.Count > 0)
                RefreshSelectionVisuals();

            NotifySelectionChanged();
        }

        /// <summary>필터 결과에 보이는 모든 태그를 선택합니다.</summary>
        public void SelectAllVisible()
        {
            if (selectionMode != GameplayTagTreeSelectionMode.Manager || visibleRows.Count == 0)
                return;

            selectedTagNames.Clear();
            foreach (GameplayTagTreeNode node in visibleRows)
                selectedTagNames.Add(node.Tag.Name);

            lastClickedRowIndex = visibleRows.Count - 1;
            primaryTag = visibleRows[lastClickedRowIndex].Tag;
            RefreshSelectionVisuals();
            NotifySelectionChanged();
        }

        /// <summary>다중 선택 모드에서 명시적으로 선택된 태그 목록을 반영합니다.</summary>
        public void SetExplicitTags(IEnumerable<string> tagNames)
        {
            explicitTags.Clear();
            implicitTags.Clear();

            if (tagNames == null)
            {
                Rebuild();
                return;
            }

            foreach (string tagName in tagNames)
            {
                if (string.IsNullOrEmpty(tagName))
                    continue;

                explicitTags.Add(tagName);
            }

            PruneRedundantExplicitTags(explicitTags);

            foreach (string tagName in explicitTags)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(tagName, logWarningIfNotFound: false);
                if (!tag.IsValid)
                    continue;

                foreach (GameplayTag parent in tag.ParentTags)
                    implicitTags.Add(parent.Name);
            }

            Rebuild();
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

        /// <summary>현재 필터·검색 조건으로 트리 행을 다시 구성합니다.</summary>
        public void Rebuild()
        {
            roots.Clear();
            roots.AddRange(GameplayTagTreeBuilder.BuildRoots(parentFilter, search, sourceFileFilter));

            foreach (GameplayTagTreeNode node in CollectAllNodes(roots))
            {
                if (expandedByRuntimeIndex.TryGetValue(node.Tag.RuntimeIndex, out bool expanded))
                    node.IsExpanded = expanded;
            }

            bool flatten = !string.IsNullOrEmpty(search);
            GameplayTagTreeBuilder.CollectVisibleRows(roots, visibleRows, flatten);

            rowsByRuntimeIndex.Clear();
            rowsHost.Clear();

            if (visibleRows.Count == 0)
            {
                string emptyMessage = GetEmptyTreeMessage();
                Label empty = new(emptyMessage)
                {
                    pickingMode = PickingMode.Ignore
                };
                empty.AddToClassList(GameplayTagEditorStyles.EmptyStateClass);
                rowsHost.Add(empty);
                return;
            }

            for (int i = 0; i < visibleRows.Count; i++)
            {
                GameplayTagTreeNode node = visibleRows[i];
                VisualElement row = CreateRow(node, flatten, i);
                rowsByRuntimeIndex[node.Tag.RuntimeIndex] = row;
                rowsHost.Add(row);
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
            foreach (VisualElement row in rowsByRuntimeIndex.Values)
            {
                if (row.userData is not string tagName)
                    continue;

                row.EnableInClassList(GameplayTagEditorStyles.TreeRowSelectedClass, selectedTagNames.Contains(tagName));
            }
        }

        private VisualElement CreateRow(GameplayTagTreeNode node, bool flattenSearch, int rowIndex)
        {
            VisualElement row = new() { name = "tag-row", userData = node.Tag.Name };
            row.AddToClassList(GameplayTagEditorStyles.TreeRowClass);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            if (selectedTagNames.Contains(node.Tag.Name))
                row.AddToClassList(GameplayTagEditorStyles.TreeRowSelectedClass);

            int depth = GameplayTagTreeBuilder.GetDepth(node);
            for (int i = 0; i < depth; i++)
            {
                VisualElement indent = new();
                indent.AddToClassList(GameplayTagEditorStyles.TreeIndentClass);
                row.Add(indent);
            }

            if (!flattenSearch && node.Children.Count > 0)
            {
                Button foldout = new()
                {
                    text = node.IsExpanded ? "▼" : "▶"
                };
                foldout.AddToClassList(GameplayTagEditorStyles.TreeFoldoutClass);
                foldout.clicked += () =>
                {
                    node.IsExpanded = !node.IsExpanded;
                    expandedByRuntimeIndex[node.Tag.RuntimeIndex] = node.IsExpanded;
                    Rebuild();
                };
                foldout.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                row.Add(foldout);
            }
            else
            {
                VisualElement spacer = new();
                spacer.style.width = 18;
                row.Add(spacer);
            }

            if (selectionMode == GameplayTagTreeSelectionMode.PickerMulti)
            {
                bool isExplicit = explicitTags.Contains(node.Tag.Name);
                bool isImplicit = implicitTags.Contains(node.Tag.Name);
                bool hasExplicitDescendant = HasExplicitDescendant(node.Tag.Name);
                Toggle toggle = new() { value = isExplicit || isImplicit };
                toggle.showMixedValue = isImplicit && !isExplicit;
                toggle.SetEnabled(!hasExplicitDescendant);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    NotifyTagInspected(node.Tag);
                    TagToggled?.Invoke(node.Tag, evt.newValue);
                    toggle.schedule.Execute(() => toggle.panel?.focusController?.focusedElement?.Blur());
                });
                row.Add(toggle);
            }

            Label label = new(GameplayTagTreeBuilder.GetRowLabel(node.Tag, flattenSearch, selectionMode))
            {
                tooltip = BuildRowTooltip(node.Tag)
            };
            label.AddToClassList(GameplayTagEditorStyles.TreeLabelClass);
            row.Add(label);

            string source = GameplayTagTreeBuilder.GetSourceLabel(node.Tag);
            if (!string.IsNullOrEmpty(source))
            {
                Label sourceLabel = new(source);
                sourceLabel.AddToClassList(GameplayTagEditorStyles.TreeSourceClass);
                row.Add(sourceLabel);
            }

            if (selectionMode != GameplayTagTreeSelectionMode.Manager)
            {
                row.RegisterCallback<MouseEnterEvent>(_ => NotifyTagInspected(node.Tag));
                row.RegisterCallback<PointerDownEvent>(_ => NotifyTagInspected(node.Tag));
            }

            if (selectionMode == GameplayTagTreeSelectionMode.Manager ||
                selectionMode == GameplayTagTreeSelectionMode.PickerSingle)
            {
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    if (selectionMode == GameplayTagTreeSelectionMode.Manager)
                        HandleManagerPointerDown(evt, rowIndex, node.Tag);
                    else
                        HandlePickerSingleClick(node.Tag);

                    evt.StopPropagation();
                });

                row.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (!isDragSelecting || selectionMode != GameplayTagTreeSelectionMode.Manager)
                        return;

                    ApplyDragSelection(dragAnchorRowIndex, rowIndex);
                });
            }

            return row;
        }

        private static string BuildRowTooltip(GameplayTag tag)
        {
            string source = GameplayTagTreeBuilder.GetSourceLabel(tag);
            if (string.IsNullOrEmpty(tag.Description))
            {
                return string.IsNullOrEmpty(source)
                    ? tag.Name
                    : $"{tag.Name}\n{source}";
            }

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

        private void OnDragSelectMouseUp(MouseUpEvent evt)
        {
            if (!isDragSelecting)
                return;

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
            selectedTagNames.Clear();
            SelectRowRange(anchorIndex, currentIndex);
            primaryTag = visibleRows[currentIndex].Tag;
            RefreshSelectionVisuals();
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            List<GameplayTag> selected = new();
            foreach (string tagName in selectedTagNames)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(tagName, logWarningIfNotFound: false);
                if (tag.IsValid)
                    selected.Add(tag);
            }

            SelectionChanged?.Invoke(selected);
        }

        private static IEnumerable<GameplayTagTreeNode> CollectAllNodes(IEnumerable<GameplayTagTreeNode> roots)
        {
            foreach (GameplayTagTreeNode root in roots)
            {
                yield return root;
                foreach (GameplayTagTreeNode child in CollectAllNodes(root.Children))
                    yield return child;
            }
        }
    }
}
