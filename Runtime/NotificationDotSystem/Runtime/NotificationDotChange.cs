using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime
{
    /// <summary>알림닷 개수가 변경된 결과입니다.</summary>
    public readonly struct NotificationDotChange
    {
        internal NotificationDotChange(string key, int previousCount, int count)
        {
            Key = key;
            PreviousCount = previousCount;
            Count = count;
        }

        public string Key { get; }
        public int PreviousCount { get; }
        public int Count { get; }
        public bool IsActive => Count > 0;
    }


    public enum NotificationDotDependencyMode
    {
        Active,
        Count
    }

    internal readonly struct NotificationDotDependency
    {
        internal NotificationDotDependency(
            string sourceKey,
            NotificationDotDependencyMode mode = NotificationDotDependencyMode.Active)
        {
            SourceKey = NotificationDotSystem.NormalizeKey(sourceKey);
            Mode = mode;
        }

        internal string SourceKey { get; }
        internal NotificationDotDependencyMode Mode { get; }
    }

    /// <summary>알림닷의 동작과 UI 표현 정보를 정의합니다.</summary>
    public sealed class NotificationDotDefinition
    {
        private readonly List<NotificationDotDependency> dependencies = new();
        private bool readOnly;

        public NotificationDotDefinition(string key)
        {
            Key = NotificationDotSystem.NormalizeKey(key);
        }

        public string Key { get; }
        public bool ClearsOnVisit { get; private set; }
        public string ViewKey { get; private set; } = string.Empty;
        internal IReadOnlyList<NotificationDotDependency> Dependencies => dependencies;

        public NotificationDotDefinition ClearOnVisit(bool enabled = true)
        {
            EnsureEditable();
            ClearsOnVisit = enabled;
            return this;
        }

        public NotificationDotDefinition UseView(string viewKey)
        {
            EnsureEditable();
            ViewKey = viewKey?.Trim() ?? string.Empty;
            return this;
        }

        public NotificationDotDefinition DependsOn(
            string sourceKey,
            NotificationDotDependencyMode mode = NotificationDotDependencyMode.Active)
        {
            EnsureEditable();
            dependencies.Add(new NotificationDotDependency(sourceKey, mode));
            return this;
        }

        public NotificationDotDefinition DependsOn<TEnum>(
            TEnum source,
            NotificationDotDependencyMode mode = NotificationDotDependencyMode.Active)
            where TEnum : struct, Enum =>
            DependsOn(NotificationDotEnum.GetKey(source), mode);

        internal NotificationDotDefinition FreezeCopy()
        {
            var copy = new NotificationDotDefinition(Key)
            {
                ClearsOnVisit = ClearsOnVisit,
                ViewKey = ViewKey,
                readOnly = true
            };

            copy.dependencies.AddRange(dependencies);
            return copy;
        }

        private void EnsureEditable()
        {
            if (readOnly)
                throw new InvalidOperationException("Registered notification definitions cannot be changed.");
        }
    }
}
