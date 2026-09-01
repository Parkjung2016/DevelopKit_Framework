using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.ProjectBrowser
{
    /// <summary>
    /// Project 창에서 방문한 폴더를 기록하고 이전/다음 폴더로 이동합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectBrowserHistory
    {
        private const int MaxHistoryCount = 100;

        private static readonly Type BrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
        private static readonly MethodInfo GetActiveFolderMethod = typeof(ProjectWindowUtil).GetMethod(
            "GetActiveFolderPath",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo ShowFolderMethod = BrowserType?.GetMethod(
            "ShowFolderContents",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly List<string> FolderHistory = new();

        private static EditorWindow lastBrowser;
        private static Delegate globalEventCallback;
        private static string currentFolder;
        private static int historyIndex = -1;

        static ProjectBrowserHistory()
        {
            RegisterGlobalEventHandler();
            EditorApplication.update += PollActiveFolder;
            EditorApplication.delayCall += ResetHistory;
        }

        private static void RegisterGlobalEventHandler()
        {
            FieldInfo eventField = typeof(EditorApplication).GetField(
                "globalEventHandler",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo callbackMethod = typeof(ProjectBrowserHistory).GetMethod(
                nameof(HandleGlobalEvent),
                BindingFlags.Static | BindingFlags.NonPublic);

            if (eventField == null || callbackMethod == null)
                return;

            globalEventCallback = Delegate.CreateDelegate(eventField.FieldType, callbackMethod);
            Delegate callbacks = eventField.GetValue(null) as Delegate;
            eventField.SetValue(null, Delegate.Combine(callbacks, globalEventCallback));
        }

        private static void HandleGlobalEvent()
        {
            Event evt = Event.current;
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (evt == null || !IsProjectBrowser(focusedWindow))
                return;

            lastBrowser = focusedWindow;

            bool back = evt.type == EventType.MouseDown && evt.button == 3;
            bool forward = evt.type == EventType.MouseDown && evt.button == 4;

            if (evt.type == EventType.KeyDown && evt.alt && !EditorGUIUtility.editingTextField)
            {
                back |= evt.keyCode == KeyCode.LeftArrow;
                forward |= evt.keyCode == KeyCode.RightArrow;
            }

            if (!back && !forward)
                return;

            CaptureActiveFolder();
            if (back && Navigate(-1))
                evt.Use();
            else if (forward && Navigate(1))
                evt.Use();
        }

        private static void PollActiveFolder()
        {
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (!IsProjectBrowser(focusedWindow))
                return;

            lastBrowser = focusedWindow;
            CaptureActiveFolder();
        }

        private static void CaptureActiveFolder()
        {
            string folder = NormalizePath(GetActiveFolderPath());
            if (string.IsNullOrEmpty(folder) || folder == currentFolder)
                return;

            currentFolder = folder;
            AddHistory(folder);
        }

        private static void ResetHistory()
        {
            FolderHistory.Clear();
            historyIndex = -1;
            currentFolder = NormalizePath(GetActiveFolderPath());

            if (!string.IsNullOrEmpty(currentFolder))
                AddHistory(currentFolder);
        }

        private static void AddHistory(string folder)
        {
            if (historyIndex >= 0 && FolderHistory[historyIndex] == folder)
                return;

            int forwardCount = FolderHistory.Count - historyIndex - 1;
            if (forwardCount > 0)
                FolderHistory.RemoveRange(historyIndex + 1, forwardCount);

            FolderHistory.Add(folder);
            if (FolderHistory.Count > MaxHistoryCount)
                FolderHistory.RemoveAt(0);

            historyIndex = FolderHistory.Count - 1;
        }

        private static bool Navigate(int direction)
        {
            int nextIndex = historyIndex + direction;
            while (nextIndex >= 0 && nextIndex < FolderHistory.Count)
            {
                string folder = FolderHistory[nextIndex];
                if (AssetDatabase.IsValidFolder(folder))
                {
                    if (OpenFolder(folder))
                    {
                        historyIndex = nextIndex;
                        return true;
                    }

                    return false;
                }

                FolderHistory.RemoveAt(nextIndex);
                if (nextIndex <= historyIndex)
                    historyIndex--;

                nextIndex = direction < 0 ? historyIndex - 1 : historyIndex + 1;
            }

            return false;
        }

        private static bool OpenFolder(string folder)
        {
            EditorWindow browser = IsProjectBrowser(EditorWindow.focusedWindow)
                ? EditorWindow.focusedWindow
                : lastBrowser;
            if (!IsProjectBrowser(browser) || ShowFolderMethod == null)
                return false;

            UnityEngine.Object folderAsset = AssetDatabase.LoadMainAssetAtPath(folder);
            if (folderAsset == null)
                return false;

            object folderId = folderAsset.GetEntityId();
            ShowFolderMethod.Invoke(browser, new[] { folderId, (object)true });
            currentFolder = folder;
            browser.Repaint();
            return true;
        }

        private static string GetActiveFolderPath() =>
            GetActiveFolderMethod?.Invoke(null, null) as string;

        private static bool IsProjectBrowser(EditorWindow window) =>
            window != null && BrowserType != null && BrowserType.IsInstanceOfType(window);

        private static string NormalizePath(string path) =>
            string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
    }
}
