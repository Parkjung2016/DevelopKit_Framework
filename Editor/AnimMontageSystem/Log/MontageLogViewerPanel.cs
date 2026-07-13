using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageLogViewerPanel : VisualElement
    {
        private const int MaxEntries = 500;
        private static readonly MethodInfo ConsoleLogCountMethod = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.LogEntries")?
            .GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly List<LogEntry> entries = new();
        private readonly ScrollView scrollView = new(ScrollViewMode.Vertical);
        private readonly ToolbarSearchField searchField = new();
        private readonly ToolbarToggle infoToggle = new() { text = "Info", value = true };
        private readonly ToolbarToggle warningToggle = new() { text = "Warn", value = true };
        private readonly ToolbarToggle errorToggle = new() { text = "Error", value = true };
        private bool rebuildQueued;
        private int lastConsoleLogCount = -1;
        private bool queuedScrollToEnd;

        public MontageLogViewerPanel()
        {
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.minHeight = 120;
            style.overflow = Overflow.Hidden;
            style.flexDirection = FlexDirection.Column;

            Add(BuildHeader());
            Add(BuildFilterBar());

            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.minHeight = 0;
            scrollView.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
            Add(scrollView);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                lastConsoleLogCount = GetConsoleLogCount();
                Application.logMessageReceived += OnLogMessageReceived;
                EditorApplication.update += OnEditorUpdate;
            });

            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                EditorApplication.update -= OnEditorUpdate;
            });
        }

        private VisualElement BuildHeader()
        {
            VisualElement header = MontageEditorLayoutHelper.CreatePanelHeader("Log Viewer");
            var spacer = new VisualElement { style = { flexGrow = 1 } };
            header.Add(spacer);

            var clearButton = new ToolbarButton(ClearEntries) { text = "Clear" };
            clearButton.style.height = 18;
            clearButton.style.marginLeft = 6;
            header.Add(clearButton);
            return header;
        }

        private VisualElement BuildFilterBar()
        {
            var toolbar = new Toolbar();
            toolbar.style.flexShrink = 0;
            toolbar.style.minHeight = 24;
            toolbar.style.paddingLeft = 4;
            toolbar.style.paddingRight = 4;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(1f, 1f, 1f, 0.06f);

            searchField.style.flexGrow = 1;
            searchField.RegisterValueChangedCallback(_ => RequestRebuild());
            toolbar.Add(searchField);

            AddToggle(toolbar, infoToggle);
            AddToggle(toolbar, warningToggle);
            AddToggle(toolbar, errorToggle);
            return toolbar;
        }

        private void AddToggle(Toolbar toolbar, ToolbarToggle toggle)
        {
            toggle.style.flexShrink = 0;
            toggle.style.marginLeft = 4;
            toggle.RegisterValueChangedCallback(_ => RequestRebuild());
            toolbar.Add(toggle);
        }

        private void OnEditorUpdate()
        {
            int consoleLogCount = GetConsoleLogCount();
            if (consoleLogCount < 0)
                return;

            if (lastConsoleLogCount >= 0 && consoleLogCount < lastConsoleLogCount && entries.Count > 0)
            {
                entries.Clear();
                RequestRebuild();
            }

            lastConsoleLogCount = consoleLogCount;
        }

        private static int GetConsoleLogCount()
        {
            if (ConsoleLogCountMethod == null)
                return -1;

            return ConsoleLogCountMethod.Invoke(null, null) is int count ? count : -1;
        }
        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            entries.Add(new LogEntry(DateTime.Now, condition, stackTrace, type));
            if (entries.Count > MaxEntries)
                entries.RemoveRange(0, entries.Count - MaxEntries);

            RequestRebuild(scrollToEnd: true);
        }

        private void ClearEntries()
        {
            entries.Clear();
            RequestRebuild();
        }

        private void RequestRebuild(bool scrollToEnd = false)
        {
            queuedScrollToEnd |= scrollToEnd;
            if (rebuildQueued)
                return;

            rebuildQueued = true;
            schedule.Execute(() =>
            {
                rebuildQueued = false;
                bool shouldScrollToEnd = queuedScrollToEnd;
                queuedScrollToEnd = false;
                Rebuild(shouldScrollToEnd);
            });
        }

        private void Rebuild(bool scrollToEnd = false)
        {
            scrollView.Clear();
            string filter = searchField.value?.Trim() ?? string.Empty;

            for (int i = 0; i < entries.Count; i++)
            {
                LogEntry entry = entries[i];
                if (!ShouldShow(entry, filter))
                    continue;

                scrollView.Add(CreateRow(entry));
            }

            if (scrollToEnd && scrollView.childCount > 0)
                scrollView.schedule.Execute(() => scrollView.ScrollTo(scrollView[scrollView.childCount - 1]));
        }

        private bool ShouldShow(LogEntry entry, string filter)
        {
            if (!IsTypeEnabled(entry.Type))
                return false;

            return string.IsNullOrEmpty(filter)
                || entry.Message.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsTypeEnabled(LogType type)
        {
            return type switch
            {
                LogType.Warning => warningToggle.value,
                LogType.Error or LogType.Assert or LogType.Exception => errorToggle.value,
                _ => infoToggle.value
            };
        }

        private VisualElement CreateRow(LogEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.035f);

            var time = new Label(entry.Time.ToString("HH:mm:ss.fff"));
            time.style.width = 78;
            time.style.flexShrink = 0;
            time.style.fontSize = 10;
            time.style.color = new Color(1f, 1f, 1f, 0.45f);
            row.Add(time);

            var level = new Label(GetLevelText(entry.Type));
            level.style.width = 44;
            level.style.flexShrink = 0;
            level.style.fontSize = 10;
            level.style.unityFontStyleAndWeight = FontStyle.Bold;
            level.style.color = GetLevelColor(entry.Type);
            row.Add(level);

            var message = new Label(entry.Message);
            message.style.flexGrow = 1;
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.fontSize = 11;
            message.style.color = new Color(1f, 1f, 1f, 0.82f);
            row.Add(message);

            return row;
        }

        private static string GetLevelText(LogType type)
        {
            return type switch
            {
                LogType.Warning => "WARN",
                LogType.Error => "ERR",
                LogType.Assert => "ASSERT",
                LogType.Exception => "EXC",
                _ => "LOG"
            };
        }

        private static Color GetLevelColor(LogType type)
        {
            return type switch
            {
                LogType.Warning => new Color(1f, 0.76f, 0.25f, 1f),
                LogType.Error or LogType.Assert or LogType.Exception => new Color(1f, 0.42f, 0.38f, 1f),
                _ => new Color(0.55f, 0.78f, 1f, 1f)
            };
        }

        private readonly struct LogEntry
        {
            public LogEntry(DateTime time, string message, string stackTrace, LogType type)
            {
                Time = time;
                Message = message ?? string.Empty;
                StackTrace = stackTrace ?? string.Empty;
                Type = type;
            }

            public DateTime Time { get; }
            public string Message { get; }
            public string StackTrace { get; }
            public LogType Type { get; }
        }
    }
}