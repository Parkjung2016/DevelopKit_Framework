using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>태그 트리 UI의 선택·표시 모드입니다.</summary>
    internal enum GameplayTagTreeSelectionMode
    {
        Manager,
        PickerSingle,
        PickerMulti
    }

    /// <summary>태그 계층 트리의 단일 노드입니다.</summary>
    internal sealed class GameplayTagTreeNode
    {
        public GameplayTag Tag;
        public readonly List<GameplayTagTreeNode> Children = new();
        public GameplayTagTreeNode Parent;
        public bool IsExpanded = true;
    }

    /// <summary>런타임 태그 목록에서 에디터 트리 데이터를 구성합니다.</summary>
    internal static class GameplayTagTreeBuilder
    {
        /// <summary>필터·검색 조건에 맞는 트리 루트 노드를 만듭니다.</summary>
        public static List<GameplayTagTreeNode> BuildRoots(
            string parentFilter = null,
            string search = null,
            string sourceFileFilter = null)
        {
            Dictionary<int, GameplayTagTreeNode> nodesByRuntimeIndex = new();
            List<GameplayTagTreeNode> roots = new();

            bool hasFilter = !string.IsNullOrEmpty(parentFilter);
            string prefix = hasFilter ? parentFilter + "." : null;

            foreach (GameplayTag tag in GameplayTagManager.GetAllTags())
            {
                if (tag.Name.StartsWith("Test.", StringComparison.Ordinal) || tag.Name.Equals("Test", StringComparison.Ordinal))
                    continue;

                if (hasFilter && !tag.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
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

                nodesByRuntimeIndex[tag.RuntimeIndex] = new GameplayTagTreeNode { Tag = tag };
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
                    roots.Add(node);
                }
            }

            SortRecursive(roots);
            return roots;
        }

        /// <summary>펼침·검색 상태에 따라 화면에 표시할 노드 행을 수집합니다.</summary>
        public static void CollectVisibleRows(IReadOnlyList<GameplayTagTreeNode> roots, List<GameplayTagTreeNode> output, bool flattenSearch)
        {
            output.Clear();
            foreach (GameplayTagTreeNode root in roots)
                Walk(root, output, flattenSearch);
        }

        private static void Walk(GameplayTagTreeNode node, List<GameplayTagTreeNode> output, bool flattenSearch)
        {
            output.Add(node);

            if (flattenSearch || node.IsExpanded)
            {
                foreach (GameplayTagTreeNode child in node.Children)
                    Walk(child, output, flattenSearch);
            }
        }

        private static void SortRecursive(List<GameplayTagTreeNode> nodes)
        {
            nodes.Sort((a, b) => string.Compare(a.Tag.Name, b.Tag.Name, StringComparison.OrdinalIgnoreCase));
            foreach (GameplayTagTreeNode node in nodes)
                SortRecursive(node.Children);
        }

        /// <summary>루트 기준 노드의 들여쓰기 깊이를 반환합니다.</summary>
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

        /// <summary>트리 행에 표시할 태그 라벨 문자열을 반환합니다.</summary>
        public static string GetRowLabel(GameplayTag tag, bool flattenSearch, GameplayTagTreeSelectionMode mode)
        {
            if (flattenSearch)
                return tag.Name;

            return tag.Label;
        }

        /// <summary>태그가 속한 JSON 소스 파일 이름(또는 다중 소스 표시)을 반환합니다.</summary>
        public static string GetSourceLabel(GameplayTag tag)
        {
            if (tag.Definition.SourceCount == 0)
                return string.Empty;

            if (tag.Definition.SourceCount == 1)
                return tag.Definition.GetSource(0).Name;

            return GameplayTagEditorLocalization.MultipleSources;
        }

        /// <summary>태그가 에디터에서 삭제 가능한 소스를 가지는지 확인합니다.</summary>
        public static bool CanDelete(GameplayTag tag)
        {
            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is IDeleteTagHandler)
                    return true;
            }

            return false;
        }

        /// <summary>태그가 에디터에서 편집 가능한 소스를 가지는지 확인합니다.</summary>
        public static bool CanEdit(GameplayTag tag) => CanDelete(tag);
    }
}
