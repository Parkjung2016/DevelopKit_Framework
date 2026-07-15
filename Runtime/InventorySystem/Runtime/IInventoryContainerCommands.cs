namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 인벤토리 컨테이너의 상태를 변경하는 명령 계약입니다.
    /// </summary>
    public interface IInventoryContainerCommands
    {
        InventoryChangeResult TryAddItem(int itemId, int count);
        InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count);
        InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count, long instanceId);
        InventoryChangeResult TryRemoveItem(int itemId, int count);
        InventoryChangeResult TryRemoveItemFromSlot(int slotIndex, int count);
        InventoryChangeResult TryMoveSlot(int fromSlotIndex, int toSlotIndex);
        InventoryChangeResult TrySwapSlots(int slotIndexA, int slotIndexB);
        InventoryChangeResult ClearSlot(int slotIndex);
        InventoryChangeResult ClearAll();
        InventoryChangeResult TrySplitStack(int slotIndex, int splitCount);
        InventoryChangeResult TryDropItemFromSlot(int slotIndex, int count);
        InventoryChangeResult TryTradeItemFromSlot(int slotIndex, int count);
        InventoryChangeResult TryUseItem(int slotIndex, IItemUseHandler handler);
    }
}
