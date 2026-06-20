using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IInventoryContainer
    {
        string ContainerId { get; }
        ContainerKind Kind { get; }
        InventoryContainerDescriptor Descriptor { get; }
        int SlotCount { get; }
        int Revision { get; }

        InventoryChangeResult TryAddItem(int itemId, int count);
        InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count);
        InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count, long instanceId);
        InventoryChangeResult TryRemoveItem(int itemId, int count);
        InventoryChangeResult TryRemoveItemFromSlot(int slotIndex, int count);
        InventoryChangeResult TryMoveSlot(int fromSlotIndex, int toSlotIndex);
        InventoryChangeResult TrySwapSlots(int slotIndexA, int slotIndexB);
        InventoryChangeResult ClearSlot(int slotIndex);
        InventoryChangeResult ClearAll();

        bool CanAddItem(int itemId, int count, out InventoryFailReason reason, out int addableCount);
        bool CanRemoveItem(int itemId, int count, out InventoryFailReason reason);
        bool CanMoveSlot(int fromSlotIndex, int toSlotIndex, out InventoryFailReason reason);
        bool CanSplitStack(int slotIndex, int splitCount, out InventoryFailReason reason, out int targetSlotIndex);

        InventoryChangeResult TrySplitStack(int slotIndex, int splitCount);
        InventoryChangeResult TryDropItemFromSlot(int slotIndex, int count);
        InventoryChangeResult TryTradeItemFromSlot(int slotIndex, int count);
        InventoryChangeResult TryUseItem(int slotIndex, IItemUseHandler handler);

        bool HasItem(int itemId, int count = 1);
        int GetItemCount(int itemId);
        bool TryGetSlot(int slotIndex, out InventorySlot slot);
        bool IsSlotEmpty(int slotIndex);

        int GetFirstEmptySlotIndex();
        int GetOccupiedSlotCount();
        void FindSlotsWithItem(int itemId, List<int> results);
        bool TryFindStackableSlot(int itemId, out int slotIndex);
    }
}
