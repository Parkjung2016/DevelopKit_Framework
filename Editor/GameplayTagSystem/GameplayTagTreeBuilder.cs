using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    internal enum GameplayTagTreeSelectionMode
    {
        Manager,
        PickerSingle,
        PickerMulti
    }

    internal sealed class GameplayTagTreeNode
    {
        public GameplayTag Tag;
        public readonly List<GameplayTagTreeNode> Children = new();
        public GameplayTagTreeNode Parent;
        public bool IsExpanded = true;
    }

    /// <summary>등록된 태그를 에디터 트리 데이터로 구성합니다.</summary>
    internal static class GameplayTagTreeBuilder
    {
        /// <summary>필터에 맞는 태그 트리를 지정한 출력 리스트에 구성합니다.</summary>
        public static void BuildRoots(
            List<GameplayTagTreeNode> output,
            string parentFilter = null,
            string search = null,
            string sourceFileFilter = null)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();
            Dictionary<int, GameplayTagTreeNode> nodesByRuntimeIndex = new();
            bool hasParentFilter = !string.IsNullOrEmpty(parentFilter);
            string parentPrefix = hasParentFilter ? parentFilter + "." : null;

            foreach (GameplayTag tag in GameplayTagManager.GetAllTags())
            {
                if (hasParentFilter && !tag.Name.StartsWith(parentPrefix, StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrEmpty(sourceFileFilter) &&
                    !GameplayTagSourceUtility.IsTagInFile(tag, sourceFileFilter))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(search) &&
                    tag.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    tag.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                nodesByRuntimeIndex.Add(
                    tag.RuntimeIndex,
                    new GameplayTagTreeNode { Tag = tag });
            }

            foreach (GameplayTagTreeNode node in nodesByRuntimeIndex.Values)
            {
                GameplayTag parentTag = node.Tag.ParentTag;
                if (!parentTag.IsNone &&
                    nodesByRuntimeIndex.TryGetValue(parentTag.RuntimeIndex, out GameplayTagTreeNode parentNode) &&
                    GameplayTagSourceUtility.SharesFileSource(parentNode.Tag, node.Tag))
                {
                    parentNode.Children.Add(node);
                    node.Parent = parentNode;
                }
                else
                {
                    output.Add(node);
                }
            }

            SortRecursive(output);
        }

        /// <summary>현재 펼침 상태에 따라 화면에 표시할 행을 수집합니다.</summary>
        public static void CollectVisibleRows(
            IReadOnlyList<GameplayTagTreeNode> roots,
            List<GameplayTagTreeNode> output,
            bool flattenSearch)
        {
            output.Clear();
            for (int i = 0; i < roots.Count; i++)
                CollectRows(roots[i], output, flattenSearch);
        }

        private static void CollectRows(
            GameplayTagTreeNode node,
            List<GameplayTagTreeNode> output,
            bool flattenSearch)
        {
            output.Add(node);
            if (!flattenSearch && !node.IsExpanded)
                return;

            for (int i = 0; i < node.Children.Count; i++)
                CollectRows(node.Children[i], output, flattenSearch);
        }

        private static void SortRecursive(List<GameplayTagTreeNode> nodes)
        {
            nodes.Sort(static (a, b) => string.Compare(a.Tag.Name, b.Tag.Name, StringComparison.Ordinal));
            for (int i = 0; i < nodes.Count; i++)
                SortRecursive(nodes[i].Children);
        }

        public static int GetDepth(GameplayTagTreeNode node)
        {
            int depth = 0;
            while (node.Parent != null)
            {
                depth++;
                node = node.Parent;
            }

            return depth;
        }

        public static string GetRowLabel(GameplayTag tag, bool flattenSearch)
        {
            return flattenSearch ? tag.Name : tag.Label;
        }

        public static string GetSourceLabel(GameplayTag tag)
        {
            if (tag.Definition.SourceCount == 0)
                return string.Empty;
            if (tag.Definition.SourceCount == 1)
                return tag.Definition.GetSource(0).Name;
            return GameplayTagEditorLocalization.MultipleSources;
        }

        public static bool CanDelete(GameplayTag tag)
        {
            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is IDeleteTagHandler)
                    return true;
            }

            return false;
        }

        public static bool CanEdit(GameplayTag tag)
        {
            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is IGameplayTagEditHandler)
                    return true;
            }

            return false;
        }
    }
}