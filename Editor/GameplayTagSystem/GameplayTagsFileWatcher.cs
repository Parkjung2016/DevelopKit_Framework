using System.Diagnostics;
using System.IO;
using System.Threading;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>태그 JSON 변경을 모아 메인 스레드에서 한 번만 Reload합니다.</summary>
    [InitializeOnLoad]
    internal static class GameplayTagsFileWatcher
    {
        private const double ReloadDelaySeconds = 0.15d;

        private static readonly long ReloadDelayTicks =
            (long)(Stopwatch.Frequency * ReloadDelaySeconds);

        private static FileSystemWatcher fileWatcher;
        private static long lastChangeTimestamp;
        private static int reloadPending;

        static GameplayTagsFileWatcher()
        {
            Directory.CreateDirectory(FileGameplayTagSource.DirectoryPath);

            fileWatcher = new FileSystemWatcher(FileGameplayTagSource.DirectoryPath, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite |
                               NotifyFilters.FileName |
                               NotifyFilters.CreationTime |
                               NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            fileWatcher.Changed += OnFileChanged;
            fileWatcher.Created += OnFileChanged;
            fileWatcher.Deleted += OnFileChanged;
            fileWatcher.Renamed += OnFileChanged;
            fileWatcher.Error += OnWatcherError;

            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private static void OnFileChanged(object _, FileSystemEventArgs __)
        {
            Interlocked.Exchange(ref lastChangeTimestamp, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref reloadPending, 1);
        }

        private static void OnWatcherError(object _, ErrorEventArgs __)
        {
            Interlocked.Exchange(ref lastChangeTimestamp, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref reloadPending, 1);
        }

        private static void Update()
        {
            if (Volatile.Read(ref reloadPending) == 0)
                return;

            long elapsed = Stopwatch.GetTimestamp() - Interlocked.Read(ref lastChangeTimestamp);
            if (elapsed < ReloadDelayTicks)
                return;

            if (Interlocked.Exchange(ref reloadPending, 0) == 0)
                return;

            GameplayTagManager.ReloadTags();
        }

        private static void Dispose()
        {
            EditorApplication.update -= Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;

            if (fileWatcher == null)
                return;

            fileWatcher.EnableRaisingEvents = false;
            fileWatcher.Changed -= OnFileChanged;
            fileWatcher.Created -= OnFileChanged;
            fileWatcher.Deleted -= OnFileChanged;
            fileWatcher.Renamed -= OnFileChanged;
            fileWatcher.Error -= OnWatcherError;
            fileWatcher.Dispose();
            fileWatcher = null;
        }
    }
}