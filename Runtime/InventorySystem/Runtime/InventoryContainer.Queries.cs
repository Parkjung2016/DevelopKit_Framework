using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
        #region Query

        public bool HasItem(int itemId, int count = 1) =>
            !isDisposed && InventoryBurstOperations.HasItem(ref slots, itemId, count);

        public int GetItemCount(int itemId) =>
            isDisposed ? 0 : InventoryBurstOperations.CountItem(ref slots, itemId);

        public JobHandle ScheduleGetItemCount(int itemId, NativeReference<int> result, JobHandle dependsOn = default)
        {
            var job = new CountItemJob
            {
                Slots = slots,
                ItemId = itemId,
                Result = result
            };
            return job.Schedule(dependsOn);
        }

        public JobHandle ScheduleHasItem(int itemId, int count, NativeReference<bool> result, JobHandle dependsOn = default)
        {
            var job = new HasItemJob
            {
                Slots = slots,
                ItemId = itemId,
                RequiredCount = count,
                Result = result
            };
            return job.Schedule(dependsOn);
        }

        public void GetOccupiedSlotIndices(List<int> results)
        {
            results.Clear();
            if (isDisposed)
                return;

            int slotCount = slots.Length;
            if (results.Capacity < slotCount)
                results.Capacity = slotCount;

            for (int i = 0; i < slotCount; i++)
            {
                if (!slots[i].IsEmpty)
                    results.Add(i);
            }
        }

        public InventorySlotSaveData[] ExportOccupiedSlots()
        {
            if (isDisposed)
                return Array.Empty<InventorySlotSaveData>();

            int slotCount = slots.Length;
            if (slotCount == 0)
                return Array.Empty<InventorySlotSaveData>();

            int occupiedCount = 0;
            for (int i = 0; i < slotCount; i++)
            {
                if (!slots[i].IsEmpty)
                    occupiedCount++;
            }

            if (occupiedCount == 0)
                return Array.Empty<InventorySlotSaveData>();

            var result = new InventorySlotSaveData[occupiedCount];
            int writeIndex = 0;

            for (int i = 0; i < slotCount; i++)
            {
                SlotData slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                result[writeIndex++] = new InventorySlotSaveData
                {
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Count = slot.Count,
                    InstanceId = slot.InstanceId
                };
            }

            return result;
        }

        public InventorySlot GetSlot(int slotIndex) =>
            TryGetSlot(slotIndex, out InventorySlot slot) ? slot : default;

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (isDisposed || slotIndex < 0 || slotIndex >= SlotCount)
            {
                slot = default;
                return false;
            }

            slot = InventorySlot.From(slots[slotIndex]);
            return true;
        }

        public bool IsSlotEmpty(int slotIndex) =>
            !isDisposed && slotIndex >= 0 && slotIndex < SlotCount && slots[slotIndex].IsEmpty;

        #endregion
    }
}
