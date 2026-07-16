using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>태그 이름 조합·검증 및 소스 파일별 부모 목록 생성을 담당합니다.</summary>
    internal static class GameplayTagNameComposer
    {
        public const string RootParentLabel = GameplayTagEditorLocalization.Root;

        /// <summary>부모 태그와 세그먼트 이름을 점 표기 전체 경로로 합칩니다.</summary>
        public static string Compose(string parentTagName, string segmentName)
        {
            string segment = segmentName?.Trim();
            if (string.IsNullOrEmpty(segment))
                return string.Empty;

            if (string.IsNullOrEmpty(parentTagName) || parentTagName == RootParentLabel)
                return segment;

            return $"{parentTagName}.{segment}";
        }

        /// <summary>세그먼트 이름 규칙을 검증합니다.</summary>
        public static bool IsSegmentValid(string segmentName, out string errorMessage)
        {
            errorMessage = null;
            string segment = segmentName?.Trim();

            if (string.IsNullOrEmpty(segment))
            {
                errorMessage = GameplayTagEditorLocalization.SegmentNameRequired;
                return false;
            }

            if (segment.Contains('.'))
            {
                errorMessage = GameplayTagEditorLocalization.SegmentUseParentOrFullPath;
                return false;
            }

            foreach (char c in segment)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    errorMessage = string.Format(GameplayTagEditorLocalization.SegmentInvalidCharacter, c);
                    return false;
                }
            }

            return true;
        }

        /// <summary>부모·세그먼트를 합쳐 유효한 전체 태그 이름을 만듭니다.</summary>
        public static bool TryComposeValidName(string parentTagName, string segmentName, out string fullName, out string errorMessage)
        {
            fullName = null;
            string segment = segmentName?.Trim();

            if (string.IsNullOrEmpty(segment))
            {
                errorMessage = GameplayTagEditorLocalization.SegmentNameRequired;
                return false;
            }

            bool isRoot = string.IsNullOrEmpty(parentTagName) || parentTagName == RootParentLabel;

            if (isRoot && segment.Contains('.'))
            {
                if (!GameplayTagUtility.IsNameValid(segment, out errorMessage))
                {
                    errorMessage = GameplayTagEditorUtility.LocalizeRuntimeMessage(errorMessage);
                    return false;
                }

                fullName = segment;
                return true;
            }

            if (!IsSegmentValid(segmentName, out errorMessage))
                return false;

            fullName = Compose(parentTagName, segmentName);
            if (!GameplayTagUtility.IsNameValid(fullName, out errorMessage))
            {
                errorMessage = GameplayTagEditorUtility.LocalizeRuntimeMessage(errorMessage);
                return false;
            }

            return true;
        }

        public static bool WillCreateMissingParents(string tagName)
        {
            if (!GameplayTagUtility.IsNameValid(tagName, out _))
                return false;

            string[] hierarchy = GameplayTagUtility.GetHierarchyNames(tagName);
            return hierarchy.Length > 1;
        }

        /// <summary>지정 소스 파일에서 태그 추가 폼용 부모 후보 목록을 만듭니다.</summary>
        public static List<string> BuildParentOptionsForSource(string sourceFileName, string excludeTagName = null)
        {
            List<string> options = new() { RootParentLabel };

            if (string.IsNullOrEmpty(sourceFileName))
                return options;

            string excludePrefix = string.IsNullOrEmpty(excludeTagName) ? null : excludeTagName + ".";

            List<string> tagNames = new();
            foreach (GameplayTag tag in GameplayTagManager.GetAllTags())
            {
                if (tag.Name.StartsWith("Test.", StringComparison.Ordinal) || tag.Name.Equals("Test", StringComparison.Ordinal))
                    continue;

                if (!GameplayTagSourceUtility.IsTagInFile(tag, sourceFileName))
                    continue;

                if (!string.IsNullOrEmpty(excludeTagName))
                {
                    if (string.Equals(tag.Name, excludeTagName, StringComparison.Ordinal))
                        continue;

                    if (excludePrefix != null && tag.Name.StartsWith(excludePrefix, StringComparison.Ordinal))
                        continue;
                }

                tagNames.Add(tag.Name);
            }

            tagNames.Sort(StringComparer.OrdinalIgnoreCase);
            options.AddRange(tagNames);
            return options;
        }

        public static void SplitTagName(string fullName, out string parentTagName, out string segmentName)
        {
            if (GameplayTagUtility.TryGetParentName(fullName, out string parent))
            {
                parentTagName = parent;
                segmentName = fullName.Substring(parent.Length + 1);
                return;
            }

            parentTagName = RootParentLabel;
            segmentName = fullName;
        }

        /// <summary>소스 파일에 없는 중간 부모 태그 이름을 수집합니다.</summary>
        public static List<string> GetMissingParentsInSourceFile(string tagName, string sourceFileName)
        {
            List<string> missing = new();

            if (!GameplayTagUtility.IsNameValid(tagName, out _))
                return missing;

            string[] hierarchy = GameplayTagUtility.GetHierarchyNames(tagName);
            for (int i = 0; i < hierarchy.Length - 1; i++)
            {
                GameplayTag parentTag = GameplayTagManager.RequestTag(hierarchy[i], logWarningIfNotFound: false);
                if (!parentTag.IsValid || !GameplayTagSourceUtility.IsTagInFile(parentTag, sourceFileName))
                    missing.Add(hierarchy[i]);
            }

            return missing;
        }

        public static bool IsInvalidParentForRename(string tagName, string parentTagName)
        {
            if (string.IsNullOrEmpty(parentTagName) || parentTagName == RootParentLabel)
                return false;

            if (string.Equals(parentTagName, tagName, StringComparison.Ordinal))
                return true;

            string prefix = tagName + ".";
            return parentTagName.StartsWith(prefix, StringComparison.Ordinal);
        }

        public static string BuildEditPreviewText(string parentTagName, string segmentName, string sourceFileName)
        {
            if (!TryComposeValidName(parentTagName, segmentName, out string fullName, out _))
            {
                string partial = Compose(parentTagName, segmentName);
                return string.IsNullOrEmpty(partial)
                    ? $"{GameplayTagEditorLocalization.FullName}: —"
                    : $"{GameplayTagEditorLocalization.FullName}: {partial}";
            }

            string location = string.IsNullOrEmpty(parentTagName) || parentTagName == RootParentLabel
                ? string.Format(GameplayTagEditorLocalization.TreeLocationRoot, GetDisplaySegment(fullName, parentTagName, segmentName))
                : string.Format(
                    GameplayTagEditorLocalization.TreeLocationUnderParent,
                    parentTagName,
                    GetDisplaySegment(fullName, parentTagName, segmentName));

            List<string> missing = GetMissingParentsInSourceFile(fullName, sourceFileName);
            if (missing.Count > 0)
            {
                return
                    $"{GameplayTagEditorLocalization.FullName}: {fullName}\n{location}\n{GameplayTagEditorLocalization.AutoCreate}: {string.Join(", ", missing)}";
            }

            return $"{GameplayTagEditorLocalization.FullName}: {fullName}\n{location}";
        }

        public static bool TryValidateParentInSourceFile(
            string parentTagName,
            string fullTagName,
            string sourceFileName,
            out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(parentTagName) || parentTagName == RootParentLabel)
                return true;

            GameplayTag parentTag = GameplayTagManager.RequestTag(parentTagName, logWarningIfNotFound: false);
            if (parentTag.IsValid && GameplayTagSourceUtility.IsTagInFile(parentTag, sourceFileName))
                return true;

            if (GameplayTagUtility.TryGetParentName(fullTagName, out string immediateParent) &&
                string.Equals(immediateParent, parentTagName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            errorMessage = GameplayTagEditorLocalization.ParentFileMismatch;
            return false;
        }

        private static string GetDisplaySegment(string fullName, string parentTagName, string segmentName)
        {
            if (string.IsNullOrEmpty(parentTagName) || parentTagName == RootParentLabel)
                return fullName;

            return segmentName?.Trim() ?? fullName;
        }

        public static string NormalizeSourceFileBaseName(string input)
        {
            string value = input?.Trim();
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = Path.GetFileNameWithoutExtension(value);
            return value;
        }

        public static string ToSourceFileName(string baseName)
        {
            string normalized = NormalizeSourceFileBaseName(baseName);
            if (string.IsNullOrEmpty(normalized))
                return "DefaultGameplayTags.json";

            return $"{normalized}.json";
        }
    }
}
