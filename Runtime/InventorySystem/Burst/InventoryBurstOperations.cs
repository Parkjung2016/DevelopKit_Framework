using Unity.Burst;
using Unity.Collections;

namespace PJDev.DevelopKit.Framework.InventorySystem.Burst
{
    [BurstCompile]
    public static class InventoryBurstOperations
    {
        [BurstCompile]
        public static int CountItem(ref NativeArray<SlotData> slots, int itemId)
        {
            if (itemId <= 0)
                return 0;

            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.ItemId == itemId)
                    total += slot.Count;
            }

            return total;
        }

        [BurstCompile]
        public static bool HasItem(ref NativeArray<SlotData> slots, int itemId, int count)
        {
            if (itemId <= 0)
                return count <= 0;

            if (count <= 0)
                return true;

            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.ItemId != itemId)
                    continue;

                total += slot.Count;
                if (total >= count)
                    return true;
            }

            return false;
        }

        [BurstCompile]
        public static void TryAddItem(
            ref NativeArray<SlotData> slots,
            int itemId,
            int count,
            int maxStackSize,
            bool isStackable,
            ref NativeList<int> changedSlots,
            out int addedTotal,
            out int remainder,
            out int totalItemCountBefore)
        {
            changedSlots.Clear();
            addedTotal = 0;
            remainder = count;
            totalItemCountBefore = 0;

            int effectiveMaxStackSize = GetEffectiveMaxStackSize(maxStackSize, isStackable);
            if (itemId <= 0 || count <= 0 || effectiveMaxStackSize <= 0)
                return;

            if (isStackable)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    SlotData slot = slots[i];
                    if (slot.ItemId == itemId)
                        totalItemCountBefore += slot.Count;
                }

                for (int i = 0; i < slots.Length && remainder > 0; i++)
                {
                    SlotData slot = slots[i];
                    if (slot.IsEmpty || slot.ItemId != itemId || slot.InstanceId != 0)
                        continue;

                    int before = remainder;
                    remainder = TryAddToSlot(ref slot, itemId, remainder, effectiveMaxStackSize);
                    if (before == remainder)
                        continue;

                    slots[i] = slot;
                    addedTotal += before - remainder;
                    changedSlots.Add(i);
                }
            }

            for (int i = 0; i < slots.Length && remainder > 0; i++)
            {
                SlotData slot = slots[i];
                if (!slot.IsEmpty)
                    continue;

                int before = remainder;
                remainder = TryAddToSlot(ref slot, itemId, remainder, effectiveMaxStackSize);
                if (before == remainder)
                    continue;

                slots[i] = slot;
                addedTotal += before - remainder;
                changedSlots.Add(i);
            }
        }

        [BurstCompile]
        public static bool TryAddItemToSlot(
            ref NativeArray<SlotData> slots,
            int slotIndex,
            int itemId,
            int count,
            int maxStackSize,
            bool isStackable,
            ref NativeList<int> changedSlots,
            bool resetChangedSlots,
            out int addedTotal,
            out int remainder)
        {
            if (resetChangedSlots)
                changedSlots.Clear();
            addedTotal = 0;
            remainder = count;

            int effectiveMaxStackSize = GetEffectiveMaxStackSize(maxStackSize, isStackable);
            if (slotIndex < 0 || slotIndex >= slots.Length || itemId <= 0 || count <= 0 || effectiveMaxStackSize <= 0)
                return false;

            SlotData slot = slots[slotIndex];
            if (!slot.IsEmpty && slot.ItemId != itemId)
                return false;

            if (!isStackable && !slot.IsEmpty)
                return false;

            if (!slot.IsEmpty && slot.InstanceId != 0)
                return false;

            int before = remainder;
            remainder = TryAddToSlot(ref slot, itemId, remainder, effectiveMaxStackSize);
            if (before == remainder)
                return false;

            slots[slotIndex] = slot;
            addedTotal = before - remainder;
            changedSlots.Add(slotIndex);
            return true;
        }

        [BurstCompile]
        public static void TryRemoveItem(
            ref NativeArray<SlotData> slots,
            int itemId,
            int count,
            ref NativeList<int> changedSlots,
            out int removedCount,
            out int remainder,
            out int totalItemCountBefore)
        {
            changedSlots.Clear();
            removedCount = 0;
            remainder = count;
            totalItemCountBefore = 0;

            if (itemId <= 0 || count <= 0)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.ItemId == itemId)
                    totalItemCountBefore += slot.Count;
            }

            if (totalItemCountBefore <= 0)
                return;

            for (int i = 0; i < slots.Length && removedCount < count; i++)
            {
                SlotData slot = slots[i];
                if (slot.IsEmpty || slot.ItemId != itemId)
                    continue;

                int removed = TryRemoveFromSlot(ref slot, count - removedCount);
                if (removed <= 0)
                    continue;

                slots[i] = slot;
                removedCount += removed;
                changedSlots.Add(i);
            }

            remainder = count - removedCount;
        }

        [BurstCompile]
        public static bool TryRemoveItemFromSlot(
            ref NativeArray<SlotData> slots,
            int slotIndex,
            int count,
            ref NativeList<int> changedSlots,
            out int removedCount,
            out int remainder)
        {
            changedSlots.Clear();
            removedCount = 0;
            remainder = count;

            if (slotIndex < 0 || slotIndex >= slots.Length || count <= 0)
                return false;

            SlotData slot = slots[slotIndex];
            if (slot.IsEmpty)
                return false;

            removedCount = TryRemoveFromSlot(ref slot, count);
            if (removedCount <= 0)
                return false;

            slots[slotIndex] = slot;
            remainder = count - removedCount;
            changedSlots.Add(slotIndex);
            return true;
        }

        [BurstCompile]
        public static bool TryMoveSlot(
            ref NativeArray<SlotData> slots,
            int fromSlotIndex,
            int toSlotIndex,
            int maxStackSize,
            bool isStackable,
            ref NativeList<int> changedSlots,
            out int processedCount,
            out int remainder)
        {
            changedSlots.Clear();
            processedCount = 0;
            remainder = 0;

            int effectiveMaxStackSize = GetEffectiveMaxStackSize(maxStackSize, isStackable);

            if (fromSlotIndex < 0 || fromSlotIndex >= slots.Length ||
                toSlotIndex < 0 || toSlotIndex >= slots.Length ||
                fromSlotIndex == toSlotIndex)
                return false;

            SlotData fromSlot = slots[fromSlotIndex];
            if (fromSlot.IsEmpty)
                return false;

            SlotData toSlot = slots[toSlotIndex];
            if (toSlot.IsEmpty)
            {
                int moveCount = isStackable ? fromSlot.Count : 1;
                if (moveCount > effectiveMaxStackSize)
                    moveCount = effectiveMaxStackSize;

                toSlot.ItemId = fromSlot.ItemId;
                toSlot.Count = moveCount;
                toSlot.InstanceId = fromSlot.InstanceId;
                TryRemoveFromSlot(ref fromSlot, moveCount);

                slots[toSlotIndex] = toSlot;
                slots[fromSlotIndex] = fromSlot;
                processedCount = moveCount;
                remainder = isStackable ? 0 : fromSlot.Count;
                changedSlots.Add(fromSlotIndex);
                changedSlots.Add(toSlotIndex);
                return processedCount > 0;
            }

            if (isStackable && toSlot.ItemId == fromSlot.ItemId && toSlot.InstanceId == 0 && fromSlot.InstanceId == 0)
            {
                int fromCount = fromSlot.Count;
                int before = fromCount;
                remainder = TryAddToSlot(ref toSlot, fromSlot.ItemId, fromCount, effectiveMaxStackSize);
                processedCount = before - remainder;
                TryRemoveFromSlot(ref fromSlot, processedCount);
                slots[toSlotIndex] = toSlot;
                slots[fromSlotIndex] = fromSlot;
                changedSlots.Add(fromSlotIndex);
                changedSlots.Add(toSlotIndex);
                return processedCount > 0;
            }

            slots[fromSlotIndex] = toSlot;
            slots[toSlotIndex] = fromSlot;
            changedSlots.Add(fromSlotIndex);
            changedSlots.Add(toSlotIndex);
            return true;
        }

        [BurstCompile]
        public static bool TrySwapSlots(
            ref NativeArray<SlotData> slots,
            int slotIndexA,
            int slotIndexB,
            ref NativeList<int> changedSlots)
        {
            changedSlots.Clear();

            if (slotIndexA < 0 || slotIndexA >= slots.Length ||
                slotIndexB < 0 || slotIndexB >= slots.Length ||
                slotIndexA == slotIndexB)
                return false;

            SlotData slotA = slots[slotIndexA];
            SlotData slotB = slots[slotIndexB];
            if (slotA.IsEmpty && slotB.IsEmpty)
                return false;

            slots[slotIndexA] = slotB;
            slots[slotIndexB] = slotA;
            changedSlots.Add(slotIndexA);
            changedSlots.Add(slotIndexB);
            return true;
        }

        [BurstCompile]
        public static bool ClearSlot(
            ref NativeArray<SlotData> slots,
            int slotIndex,
            ref NativeList<int> changedSlots,
            out int clearedCount)
        {
            changedSlots.Clear();
            clearedCount = 0;

            if (slotIndex < 0 || slotIndex >= slots.Length)
                return false;

            SlotData slot = slots[slotIndex];
            if (slot.IsEmpty)
                return false;

            clearedCount = slot.Count;
            slots[slotIndex] = SlotData.Empty;
            changedSlots.Add(slotIndex);
            return true;
        }

        [BurstCompile]
        public static int ClearAll(ref NativeArray<SlotData> slots, ref NativeList<int> changedSlots)
        {
            changedSlots.Clear();
            int clearedCount = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                    continue;

                slots[i] = SlotData.Empty;
                changedSlots.Add(i);
                clearedCount++;
            }

            return clearedCount;
        }

        [BurstCompile]
        public static int SimulateAddItem(
            ref NativeArray<SlotData> slots,
            int itemId,
            int count,
            int maxStackSize,
            bool isStackable)
        {
            if (itemId <= 0 || count <= 0)
                return 0;

            int effectiveMaxStackSize = GetEffectiveMaxStackSize(maxStackSize, isStackable);
            if (effectiveMaxStackSize <= 0)
                return 0;

            int remainder = count;

            if (isStackable)
            {
                for (int i = 0; i < slots.Length && remainder > 0; i++)
                {
                    SlotData slot = slots[i];
                    if (slot.IsEmpty || slot.ItemId != itemId || slot.InstanceId != 0)
                        continue;

                    remainder = SimulateAddToSlot(ref slot, itemId, remainder, effectiveMaxStackSize);
                }
            }
            else
            {
                int uniqueSlots = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].IsEmpty)
                        uniqueSlots++;
                }

                int addable = uniqueSlots < remainder ? uniqueSlots : remainder;
                return addable;
            }

            for (int i = 0; i < slots.Length && remainder > 0; i++)
            {
                if (!slots[i].IsEmpty)
                    continue;

                SlotData slot = default;
                remainder = SimulateAddToSlot(ref slot, itemId, remainder, effectiveMaxStackSize);
            }

            return count - remainder;
        }

        [BurstCompile]
        public static bool TryPlaceItemInEmptySlot(
            ref NativeArray<SlotData> slots,
            int slotIndex,
            int itemId,
            int count,
            long instanceId,
            ref NativeList<int> changedSlots,
            bool resetChangedSlots)
        {
            if (resetChangedSlots)
                changedSlots.Clear();

            if (slotIndex < 0 || slotIndex >= slots.Length || itemId <= 0 || count <= 0)
                return false;

            if (!slots[slotIndex].IsEmpty)
                return false;

            if (instanceId != 0 && count != 1)
                return false;

            slots[slotIndex] = SlotData.Create(itemId, count, instanceId);
            changedSlots.Add(slotIndex);
            return true;
        }

        [BurstCompile]
        public static bool TrySplitStack(
            ref NativeArray<SlotData> slots,
            int fromSlotIndex,
            int toSlotIndex,
            int splitCount,
            ref NativeList<int> changedSlots,
            out int processedCount)
        {
            changedSlots.Clear();
            processedCount = 0;

            if (fromSlotIndex < 0 || fromSlotIndex >= slots.Length ||
                toSlotIndex < 0 || toSlotIndex >= slots.Length ||
                fromSlotIndex == toSlotIndex ||
                splitCount <= 0)
                return false;

            SlotData fromSlot = slots[fromSlotIndex];
            if (fromSlot.IsEmpty || fromSlot.Count <= splitCount || fromSlot.InstanceId != 0)
                return false;

            if (!slots[toSlotIndex].IsEmpty)
                return false;

            slots[toSlotIndex] = SlotData.Create(fromSlot.ItemId, splitCount, 0);
            fromSlot.Count -= splitCount;
            slots[fromSlotIndex] = fromSlot;
            processedCount = splitCount;
            changedSlots.Add(fromSlotIndex);
            changedSlots.Add(toSlotIndex);
            return true;
        }

        [BurstCompile]
        public static int FindFirstEmptySlotIndex(ref NativeArray<SlotData> slots, int startIndex = 0)
        {
            if (startIndex < 0)
                startIndex = 0;

            for (int i = startIndex; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                    return i;
            }

            for (int i = 0; i < startIndex && i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                    return i;
            }

            return -1;
        }

        [BurstCompile]
        public static int GetOccupiedSlotCount(ref NativeArray<SlotData> slots)
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                    count++;
            }

            return count;
        }

        [BurstCompile]
        private static int SimulateAddToSlot(ref SlotData slot, int itemId, int amount, int maxStackSize)
        {
            if (itemId <= 0 || amount <= 0 || maxStackSize <= 0)
                return amount;

            if (slot.IsEmpty)
            {
                int added = amount < maxStackSize ? amount : maxStackSize;
                slot.ItemId = itemId;
                slot.Count = added;
                return amount - added;
            }

            if (slot.ItemId != itemId || slot.InstanceId != 0)
                return amount;

            int remainingSpace = maxStackSize - slot.Count;
            if (remainingSpace <= 0)
                return amount;

            int addable = amount < remainingSpace ? amount : remainingSpace;
            slot.Count += addable;
            return amount - addable;
        }

        [BurstCompile]
        private static int GetEffectiveMaxStackSize(int maxStackSize, bool isStackable) =>
            isStackable ? maxStackSize : 1;

        [BurstCompile]
        private static int TryAddToSlot(ref SlotData slot, int itemId, int amount, int maxStackSize)
        {
            if (itemId <= 0 || amount <= 0 || maxStackSize <= 0)
                return amount;

            if (slot.IsEmpty)
            {
                int added = amount < maxStackSize ? amount : maxStackSize;
                slot.ItemId = itemId;
                slot.Count = added;
                return amount - added;
            }

            if (slot.ItemId != itemId)
                return amount;

            if (slot.InstanceId != 0)
                return amount;

            int remainingSpace = maxStackSize - slot.Count;
            if (remainingSpace <= 0)
                return amount;

            int addable = amount < remainingSpace ? amount : remainingSpace;
            slot.Count += addable;
            return amount - addable;
        }

        [BurstCompile]
        private static int TryRemoveFromSlot(ref SlotData slot, int amount)
        {
            if (slot.IsEmpty || amount <= 0)
                return 0;

            int removed = amount < slot.Count ? amount : slot.Count;
            slot.Count -= removed;

            if (slot.Count <= 0)
                slot = SlotData.Empty;

            return removed;
        }
    }
}
