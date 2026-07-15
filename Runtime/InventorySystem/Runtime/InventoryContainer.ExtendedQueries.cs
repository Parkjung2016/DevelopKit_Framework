using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
        #region Query

        public int GetFirstEmptySlotIndex() =>
            isDisposed ? -1 : InventoryBurstOperations.FindFirstEmptySlotIndex(ref slots);

        public int GetOccupiedSlotCount() =>
            isDisposed ? 0 : InventoryBurstOperations.GetOccupiedSlotCount(ref slots);

        public void FindSlotsWithItem(int itemId, List<int> results)
        {
            results.Clear();
            if (isDisposed || itemId <= 0)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemId == itemId)
                    results.Add(i);
            }
        }

        public bool TryFindStackableSlot(int itemId, out int slotIndex)
        {
            slotIndex = -1;
            if (isDisposed || itemId <= 0 || itemDatabase == null)
                return false;

            if (!TryGetDefinition(itemId, out ItemDefinition definition) || !definition.IsStackable)
                return false;

            int maxStack = definition.MaxStackSize;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.ItemId == itemId && slot.InstanceId == 0 && slot.Count < maxStack)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Weight / capacity helpers

        public float GetTotalWeight()
        {
            if (isDisposed || itemDatabase == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotData slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                if (!TryGetDefinition(slot.ItemId, out ItemDefinition definition))
                    continue;

                total += definition.Weight * slot.Count;
            }

            return total;
        }

        #endregion
    }
}
