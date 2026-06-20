using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace PJDev.DevelopKit.Framework.InventorySystem.Burst
{
    [BurstCompile]
    public struct CountItemJob : IJob
    {
        [ReadOnly] public NativeArray<SlotData> Slots;
        public int ItemId;
        public NativeReference<int> Result;

        public void Execute()
        {
            Result.Value = InventoryBurstOperations.CountItem(ref Slots, ItemId);
        }
    }

    [BurstCompile]
    public struct HasItemJob : IJob
    {
        [ReadOnly] public NativeArray<SlotData> Slots;
        public int ItemId;
        public int RequiredCount;
        public NativeReference<bool> Result;

        public void Execute()
        {
            Result.Value = InventoryBurstOperations.HasItem(ref Slots, ItemId, RequiredCount);
        }
    }
}
