namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime
{
    /// <summary>알림 키의 현재 상태를 조회할 때 사용하는 읽기 전용 값입니다.</summary>
    public readonly struct NotificationDotSnapshot
    {
        public NotificationDotSnapshot(string key, int directCount, int count)
        {
            Key = key;
            DirectCount = directCount;
            Count = count;
        }

        public string Key { get; }
        public int DirectCount { get; }
        public int Count { get; }
        public bool IsActive => Count > 0;
    }
}
