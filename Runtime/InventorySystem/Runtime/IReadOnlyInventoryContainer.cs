using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 인벤토리 상태를 변경하지 않고 조회할 때 사용하는 계약입니다.
    /// </summary>
    public interface IReadOnlyInventoryContainer
    {
        string ContainerId { get; }
        ContainerKind Kind { get; }
        InventoryContainerDescriptor Descriptor { get; }
        int SlotCount { get; }
        int Revision { get; }

        bool CanAddItem(int itemId, int count, out InventoryFailReason reason, out int addableCount);
        bool CanRemoveItem(int itemId, int count, out InventoryFailReason reason);
        bool CanMoveSlot(int fromSlotIndex, int toSlotIndex, out InventoryFailReason reason);
        bool CanSplitStack(int slotIndex, int splitCount, out InventoryFailReason reason, out int targetSlotIndex);

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
