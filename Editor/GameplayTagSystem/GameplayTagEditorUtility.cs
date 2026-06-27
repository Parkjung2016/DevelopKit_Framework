using System.Diagnostics;
using System.IO;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>런타임 오류 코드를 에디터용 한글 메시지로 변환합니다.</summary>
    internal static class GameplayTagEditorUtility
    {
        /// <summary>태그 JSON이 저장된 폴더를 탐색기에서 엽니다.</summary>
        public static void OpenTagsDirectory()
        {
            string path = Path.GetFullPath(FileGameplayTagSource.DirectoryPath);
            Directory.CreateDirectory(path);

#if UNITY_EDITOR_WIN
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
#else
            EditorUtility.RevealInFinder(path);
#endif
        }

        /// <summary>등록된 JSON 소스 파일이 하나 이상 있는지 확인합니다.</summary>
        public static bool HasSourceFiles()
        {
            foreach (FileGameplayTagSource _ in FileGameplayTagSource.GetAllFileSources())
                return true;

            return false;
        }

        /// <summary>IMGUI 좌표 사각형을 스크린 좌표로 변환합니다.</summary>
        public static Rect ToScreenRect(Rect guiRect)
        {
            return GUIUtility.GUIToScreenRect(guiRect);
        }

        /// <summary>런타임 태그 API 오류 문자열을 에디터용 한글 메시지로 변환합니다.</summary>
        public static string LocalizeRuntimeMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            if (message.StartsWith("TAG_NOT_IN_FILE:"))
            {
                string tagName = message.Substring("TAG_NOT_IN_FILE:".Length);
                return string.Format(GameplayTagEditorLocalization.TagNotInFile, tagName);
            }

            if (message.StartsWith("TAG_RENAME_CONFLICT:"))
            {
                string[] parts = message.Substring("TAG_RENAME_CONFLICT:".Length).Split(':');
                if (parts.Length >= 2)
                    return string.Format(GameplayTagEditorLocalization.TagRenameConflict, parts[0], parts[1]);
            }

            if (message.StartsWith("TAG_RENAME_DUPLICATE:"))
            {
                string newName = message.Substring("TAG_RENAME_DUPLICATE:".Length);
                return string.Format(GameplayTagEditorLocalization.TagRenameConflict, newName, newName);
            }

            if (message.StartsWith("TAG_ALREADY_EXISTS:"))
            {
                string[] parts = message.Substring("TAG_ALREADY_EXISTS:".Length).Split(':');
                if (parts.Length >= 2)
                    return string.Format(GameplayTagEditorLocalization.TagAlreadyExists, parts[0], parts[1]);
            }

            if (message.StartsWith("TAG_DELETE_PROMOTE_CONFLICT:"))
            {
                string[] parts = message.Substring("TAG_DELETE_PROMOTE_CONFLICT:".Length).Split(':');
                if (parts.Length >= 2)
                {
                    return string.Format(
                        GameplayTagEditorLocalization.DeletePromoteConflict,
                        parts[0],
                        parts[1]);
                }
            }

            if (message == "TAG_SOURCE_NOT_LOADED")
                return GameplayTagEditorLocalization.TagSourceNotLoaded;

            if (message.StartsWith("Tag name cannot be", System.StringComparison.Ordinal) ||
                message.StartsWith("Invalid tag name", System.StringComparison.Ordinal))
            {
                return GameplayTagEditorLocalization.InvalidTagNameDetail + "\n" + message;
            }

            return message;
        }
    }
}
