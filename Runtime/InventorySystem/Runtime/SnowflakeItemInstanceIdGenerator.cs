using System;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 64-bit Snowflake 스타일 InstanceId를 생성합니다.
    /// timestamp(41) + itemId fragment(10) + sequence(12)
    /// </summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public sealed partial class SnowflakeItemInstanceIdGenerator : IItemInstanceIdGenerator
    {
        private static SnowflakeItemInstanceIdGenerator instance = new();

        public static SnowflakeItemInstanceIdGenerator Instance => instance;

        private const int SequenceBits = 12;
        private const int ItemIdBits = 10;
        private const long SequenceMask = (1L << SequenceBits) - 1;
        private const long ItemIdMask = (1L << ItemIdBits) - 1;
        private const int TimestampShift = SequenceBits + ItemIdBits;
        private const int ItemIdShift = SequenceBits;

        private static readonly long EpochMs =
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private readonly object sync = new();
        private long lastTimestamp = -1;
        private long sequence;

        public long Generate(int itemId)
        {
            if (itemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(itemId));

            lock (sync)
            {
                long timestamp = GetTimestampMs();
                if (timestamp < lastTimestamp)
                    timestamp = lastTimestamp;

                if (timestamp == lastTimestamp)
                {
                    sequence = (sequence + 1) & SequenceMask;
                    if (sequence == 0)
                        timestamp = WaitNextTimestamp(lastTimestamp);
                }
                else
                {
                    sequence = 0;
                }

                lastTimestamp = timestamp;

                long itemFragment = (uint)itemId & ItemIdMask;
                return (timestamp << TimestampShift)
                       | (itemFragment << ItemIdShift)
                       | sequence;
            }
        }

        private static long GetTimestampMs() =>
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - EpochMs;

        private static long WaitNextTimestamp(long currentTimestamp)
        {
            long timestamp = GetTimestampMs();
            while (timestamp <= currentTimestamp)
                timestamp = GetTimestampMs();

            return timestamp;
        }
    }
}
