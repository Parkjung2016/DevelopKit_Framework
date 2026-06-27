using System;
using System.IO;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그가 어느 JSON 소스 파일에 속하는지 판별하는 유틸리티입니다.</summary>
    internal static class GameplayTagSourceUtility
    {
        /// <summary>두 태그 정의가 같은 JSON 파일 소스를 공유하는지 확인합니다.</summary>
        public static bool SharesFileSource(GameplayTagDefinition a, GameplayTagDefinition b)
        {
            if (a == null || b == null)
                return false;

            foreach (IGameplayTagSource sourceA in a.GetAllSources())
            {
                if (sourceA is not FileGameplayTagSource fileA)
                    continue;

                foreach (IGameplayTagSource sourceB in b.GetAllSources())
                {
                    if (sourceB is not FileGameplayTagSource fileB)
                        continue;

                    if (string.Equals(fileA.FilePath, fileB.FilePath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>두 태그가 같은 JSON 파일 소스를 공유하는지 확인합니다.</summary>
        public static bool SharesFileSource(GameplayTag a, GameplayTag b)
        {
            if (a.IsNone || b.IsNone)
                return false;

            return SharesFileSource(a.Definition, b.Definition);
        }

        /// <summary>태그가 등록된 첫 번째 JSON 소스 파일 이름을 반환합니다.</summary>
        public static string GetPrimaryFileSourceName(GameplayTag tag)
        {
            if (tag.IsNone)
                return null;

            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is FileGameplayTagSource fileSource)
                    return fileSource.Name;
            }

            return null;
        }

        /// <summary>태그가 등록된 JSON 소스 파일의 전체 경로를 반환합니다.</summary>
        public static bool TryGetFileSourcePath(GameplayTag tag, out string filePath)
        {
            filePath = null;

            if (tag.IsNone)
                return false;

            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is FileGameplayTagSource fileSource)
                {
                    filePath = fileSource.FilePath;
                    return true;
                }
            }

            return false;
        }

        /// <summary>태그가 지정한 소스 파일 이름(또는 경로)에 포함되어 있는지 확인합니다.</summary>
        public static bool IsTagInFile(GameplayTag tag, string sourceFileName)
        {
            if (tag.IsNone || string.IsNullOrEmpty(sourceFileName))
                return false;

            string expectedName = Path.GetFileName(sourceFileName);
            for (int i = 0; i < tag.Definition.SourceCount; i++)
            {
                if (tag.Definition.GetSource(i) is FileGameplayTagSource fileSource &&
                    string.Equals(fileSource.Name, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
