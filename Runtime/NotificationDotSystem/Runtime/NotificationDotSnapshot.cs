namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime
{
    /// <summary>알림닷의 현재 상태를 Monitor에 전달하는 내부 데이터입니다.</summary>
    internal readonly struct NotificationDotSnapshot
    {
        internal NotificationDotSnapshot(string key, int directCount, int count)
        {
            Key = key;
            DirectCount = directCount;
            Count = count;
        }

        internal string Key { get; }
        internal int DirectCount { get; }
        internal int Count { get; }
        internal bool IsActive => Count > 0;
    }
}