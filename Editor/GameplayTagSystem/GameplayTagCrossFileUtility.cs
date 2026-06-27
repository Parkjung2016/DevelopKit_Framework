using System;
using System.Collections.Generic;
using System.Text;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>다른 JSON 소스 파일과 태그 이름이 겹칠 때 이동·확인 다이얼로그를 처리합니다.</summary>
    internal static class GameplayTagCrossFileUtility
    {
        private readonly struct CrossFileConflict
        {
            public readonly string TagName;
            public readonly string SourceFileName;
            public readonly FileGameplayTagSource Source;

            public CrossFileConflict(string tagName, FileGameplayTagSource source)
            {
                TagName = tagName;
                Source = source;
                SourceFileName = source.Name;
            }
        }

        /// <summary>다른 파일에 있는 태그를 대상 소스로 옮기기 전 충돌을 확인·해결합니다.</summary>
        public static bool TryResolveTagsInOtherFilesForMove(
            FileGameplayTagSource targetSource,
            IEnumerable<string> tagNames,
            out string errorMessage)
        {
            errorMessage = null;

            if (targetSource == null)
            {
                errorMessage = GameplayTagEditorLocalization.TagSourceNotLoaded;
                return false;
            }

            List<CrossFileConflict> conflicts = CollectConflicts(targetSource.Name, tagNames);
            if (conflicts.Count == 0)
                return true;

            if (!ConfirmMoveToTarget(conflicts, targetSource.Name))
                return false;

            foreach (CrossFileConflict conflict in conflicts)
            {
                if (!conflict.Source.TryMoveTagHierarchyTo(targetSource, conflict.TagName, out errorMessage))
                {
                    errorMessage = GameplayTagEditorUtility.LocalizeRuntimeMessage(errorMessage);
                    return false;
                }
            }

            return true;
        }

        /// <summary>이름 변경 시 다른 파일의 동일 태그 충돌을 확인·해결합니다.</summary>
        public static bool TryResolveRenameConflictInOtherFile(
            string targetSourceFileName,
            string newTagName,
            out string errorMessage)
        {
            errorMessage = null;

            FileGameplayTagSource otherSource =
                FileGameplayTagSource.FindSourceContainingTag(newTagName, targetSourceFileName);

            if (otherSource == null)
                return true;

            string message = string.Format(
                GameplayTagEditorLocalization.TagCrossFileRenameConflictMessage,
                newTagName,
                otherSource.Name,
                targetSourceFileName);

            if (!EditorUtility.DisplayDialog(
                    GameplayTagEditorLocalization.TagCrossFileMoveTitle,
                    message,
                    GameplayTagEditorLocalization.TagCrossFileMove,
                    GameplayTagEditorLocalization.Cancel))
            {
                return false;
            }

            if (!otherSource.TryDeleteTag(newTagName, GameplayTagDeleteMode.Hierarchy, out errorMessage))
            {
                errorMessage = GameplayTagEditorUtility.LocalizeRuntimeMessage(errorMessage);
                return false;
            }

            return true;
        }

        /// <summary>이동·생성 시 다른 파일에 있는 관련 태그 이름을 수집합니다.</summary>
        public static List<string> CollectCrossFileTagNames(string targetSourceFileName, string tagName)
        {
            List<string> tagNames = new() { tagName };

            foreach (string parent in GameplayTagNameComposer.GetMissingParentsInSourceFile(tagName, targetSourceFileName))
            {
                if (FileGameplayTagSource.FindSourceContainingTag(parent, targetSourceFileName) != null)
                    tagNames.Add(parent);
            }

            return PruneToRootTagNames(tagNames);
        }

        private static List<CrossFileConflict> CollectConflicts(string targetSourceFileName, IEnumerable<string> tagNames)
        {
            List<CrossFileConflict> conflicts = new();

            foreach (string tagName in PruneToRootTagNames(tagNames))
            {
                FileGameplayTagSource source =
                    FileGameplayTagSource.FindSourceContainingTag(tagName, targetSourceFileName);

                if (source != null)
                    conflicts.Add(new CrossFileConflict(tagName, source));
            }

            return conflicts;
        }

        private static bool ConfirmMoveToTarget(IReadOnlyList<CrossFileConflict> conflicts, string targetSourceFileName)
        {
            string message;
            if (conflicts.Count == 1)
            {
                CrossFileConflict conflict = conflicts[0];
                message = string.Format(
                    GameplayTagEditorLocalization.TagCrossFileMoveMessage,
                    conflict.TagName,
                    conflict.SourceFileName,
                    targetSourceFileName);
            }
            else
            {
                StringBuilder lines = new();
                for (int i = 0; i < conflicts.Count; i++)
                {
                    CrossFileConflict conflict = conflicts[i];
                    lines.Append("• ");
                    lines.Append(conflict.TagName);
                    lines.Append(" (");
                    lines.Append(conflict.SourceFileName);
                    lines.Append(')');
                    if (i < conflicts.Count - 1)
                        lines.Append('\n');
                }

                message = string.Format(
                    GameplayTagEditorLocalization.TagCrossFileMoveMessageMulti,
                    lines,
                    targetSourceFileName);
            }

            return EditorUtility.DisplayDialog(
                GameplayTagEditorLocalization.TagCrossFileMoveTitle,
                message,
                GameplayTagEditorLocalization.TagCrossFileMove,
                GameplayTagEditorLocalization.Cancel);
        }

        private static List<string> PruneToRootTagNames(IEnumerable<string> tagNames)
        {
            List<string> sorted = new(tagNames);
            sorted.Sort((a, b) => a.Length.CompareTo(b.Length));

            List<string> roots = new();
            foreach (string tagName in sorted)
            {
                bool isDescendant = false;
                foreach (string root in roots)
                {
                    if (tagName.StartsWith(root + ".", StringComparison.Ordinal))
                    {
                        isDescendant = true;
                        break;
                    }
                }

                if (!isDescendant)
                    roots.Add(tagName);
            }

            return roots;
        }
    }
}
