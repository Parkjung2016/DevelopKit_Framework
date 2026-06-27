using System.IO;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>태그 JSON 파일 변경 시 런타임 태그 목록을 자동으로 다시 로드합니다.</summary>
    [InitializeOnLoad]
    internal static class GameplayTagsFileWatcher
    {
        private static FileSystemWatcher fileWatcher;

        static GameplayTagsFileWatcher()
        {
            if (!Directory.Exists(FileGameplayTagSource.DirectoryPath))
                return;

            fileWatcher = new FileSystemWatcher(FileGameplayTagSource.DirectoryPath, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            fileWatcher.Changed += OnFileChanged;
            fileWatcher.Created += OnFileChanged;
            fileWatcher.Renamed += OnFileChanged;
            fileWatcher.EnableRaisingEvents = true;
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            EditorApplication.delayCall += () => GameplayTagManager.ReloadTags();
        }
    }
}
