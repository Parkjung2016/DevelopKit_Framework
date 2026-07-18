using System;
using System.IO;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그가 등록된 파일 소스를 조회합니다.</summary>
    internal static class GameplayTagSourceUtility
    {
        /// <summary>두 태그가 같은 JSON 파일에 등록되어 있는지 확인합니다.</summary>
        public static bool HasSameFileSource(GameplayTag a, GameplayTag b)
        {
            if (a.IsNone || b.IsNone ||
                a.Definition.Source is not FileGameplayTagSource fileA ||
                b.Definition.Source is not FileGameplayTagSource fileB)
            {
                return false;
            }

            return string.Equals(fileA.FilePath, fileB.FilePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>태그가 등록된 JSON 파일 이름을 반환합니다.</summary>
        public static string GetFileSourceName(GameplayTag tag)
        {
            return tag.IsNone || tag.Definition.Source is not FileGameplayTagSource source
                ? null
                : source.Name;
        }

        /// <summary>태그가 등록된 JSON 파일의 전체 경로를 반환합니다.</summary>
        public static bool TryGetFileSourcePath(GameplayTag tag, out string filePath)
        {
            if (!tag.IsNone && tag.Definition.Source is FileGameplayTagSource source)
            {
                filePath = source.FilePath;
                return true;
            }

            filePath = null;
            return false;
        }

        /// <summary>태그가 지정한 JSON 파일에 등록되어 있는지 확인합니다.</summary>
        public static bool IsTagInFile(GameplayTag tag, string sourceFileName)
        {
            if (tag.IsNone || string.IsNullOrEmpty(sourceFileName) ||
                tag.Definition.Source is not FileGameplayTagSource source)
            {
                return false;
            }

            return string.Equals(
                source.Name,
                Path.GetFileName(sourceFileName),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}