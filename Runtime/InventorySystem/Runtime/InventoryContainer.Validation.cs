using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryContainer
    {
        #region Validation (Dry-run)

        private bool TryValidateAddRequest(
            int itemId,
            int count,
            out ItemDefinition definition,
            out InventoryFailReason reason)
        {
            definition = default;
            if (isDisposed || itemDatabase == null && !ItemCatalog.IsReady)
            {
                reason = InventoryFailReason.DatabaseNotReady;
                return false;
            }

            if (itemId <= 0)
            {
                reason = InventoryFailReason.InvalidItemId;
                return false;
            }

            if (count <= 0)
            {
                reason = InventoryFailReason.InvalidCount;
                return false;
            }

            if (!TryGetDefinition(itemId, out definition))
            {
                reason = InventoryFailReason.DefinitionNotFound;
                return false;
            }

            reason = InventoryFailReason.None;
            return true;
        }

        public bool CanAddItem(int itemId, int count, out InventoryFailReason reason, out int addableCount)
        {
            addableCount = 0;
            reason = InventoryFailReason.None;

            if (!TryValidateAddRequest(itemId, count, out ItemDefinition definition, out reason))
                return false;

            if (!PassesCapacityRule(definition, count, out reason))
                return false;

            addableCount = UsesCustomSlotRule
                ? SimulateAddWithSlotRule(itemId, count, definition)
                : InventoryBurstOperations.SimulateAddItem(
                    ref slots,
                    itemId,
                    count,
                    definition.MaxStackSize,
                    definition.IsStackable);

            if (addableCount <= 0)
            {
                reason = UsesCustomSlotRule
                    ? ResolveSlotRuleDeniedReason(definition)
                    : InventoryFailReason.NoSpace;
                return false;
            }

            reason = InventoryFailReason.None;
            return addableCount >= count;
        }

        public bool CanRemoveItem(int itemId, int count, out InventoryFailReason reason)
        {
            reason = InventoryFailReason.None;

            if (itemId <= 0)
            {
                reason = InventoryFailReason.InvalidItemId;
                return false;
            }

            if (count <= 0)
            {
                reason = InventoryFailReason.InvalidCount;
                return false;
            }

            int currentCount = GetItemCount(itemId);
            if (currentCount <= 0)
            {
                reason = InventoryFailReason.ItemNotFound;
                return false;
            }

            if (currentCount < count)
            {
                reason = InventoryFailReason.InsufficientItemCount;
                return false;
            }

            return true;
        }

        public bool CanMoveSlot(int fromSlotIndex, int toSlotIndex, out InventoryFailReason reason)
        {
            reason = InventoryFailReason.None;

            if (fromSlotIndex < 0 || fromSlotIndex >= SlotCount || toSlotIndex < 0 || toSlotIndex >= SlotCount)
            {
                reason = InventoryFailReason.InvalidSlotIndex;
                return false;
            }

            if (fromSlotIndex == toSlotIndex || slots[fromSlotIndex].IsEmpty)
            {
                reason = InventoryFailReason.NoChange;
                return false;
            }

            int itemId = slots[fromSlotIndex].ItemId;
            if (!TryGetDefinition(itemId, out ItemDefinition definition))
            {
                reason = InventoryFailReason.DefinitionNotFound;
                return false;
            }

            SlotData toSlot = slots[toSlotIndex];
            if (toSlot.IsEmpty && !CanAcceptSlot(toSlotIndex, definition))
            {
                reason = InventoryFailReason.SlotRuleDenied;
                return false;
            }

            if (!toSlot.IsEmpty && toSlot.ItemId == itemId && !CanAcceptSlot(toSlotIndex, definition))
            {
                reason = InventoryFailReason.SlotRuleDenied;
                return false;
            }

            return true;
        }

        public bool CanSplitStack(int slotIndex, int splitCount, out InventoryFailReason reason, out int targetSlotIndex)
        {
            targetSlotIndex = -1;
            reason = InventoryFailReason.None;

            if (splitCount <= 0)
            {
                reason = InventoryFailReason.InvalidCount;
                return false;
            }

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                reason = InventoryFailReason.InvalidSlotIndex;
                return false;
            }

            SlotData slot = slots[slotIndex];
            if (slot.IsEmpty || slot.Count <= splitCount || slot.InstanceId != 0)
            {
                reason = InventoryFailReason.NoChange;
                return false;
            }

            targetSlotIndex = GetFirstEmptySlotIndex();
            if (targetSlotIndex < 0)
            {
                reason = InventoryFailReason.CapacityRuleDenied;
                return false;
            }

            return true;
        }

        #endregion
    }
}
