using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary><see cref="SnowflakeItemInstanceIdGenerator"/>로 생성된 InstanceId 디코딩 헬퍼입니다.</summary>
    public static class ItemInstanceIdUtility
    {
        private const int SequenceBits = 12;
        private const int ItemIdBits = 10;
        private const int TimestampShift = SequenceBits + ItemIdBits;
        private const int ItemIdShift = SequenceBits;
        private const long ItemIdMask = (1L << ItemIdBits) - 1;
        private const long SequenceMask = (1L << SequenceBits) - 1;

        private static readonly long EpochMs =
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        public static bool IsValid(long instanceId) => instanceId > 0;

        public static bool TryGetCreatedTimeUtc(long instanceId, out DateTimeOffset createdAtUtc)
        {
            createdAtUtc = default;
            if (!IsValid(instanceId))
                return false;

            long timestamp = instanceId >> TimestampShift;
            createdAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(EpochMs + timestamp);
            return true;
        }

        /// <summary>InstanceId에 포함된 itemId 하위 10bit fragment입니다. 전체 ItemId는 슬롯/ItemInstanceData를 참조하세요.</summary>
        public static int GetItemIdFragment(long instanceId) =>
            (int)((instanceId >> ItemIdShift) & ItemIdMask);

        public static int GetSequence(long instanceId) =>
            (int)(instanceId & SequenceMask);
    }
}
