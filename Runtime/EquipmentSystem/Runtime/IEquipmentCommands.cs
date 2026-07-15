using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public interface IEquipmentCommands
    {
        InventoryChangeResult TryEquipFromContainer(
            string sourceContainerId,
            int sourceSlotIndex,
            int equipSlotIndex);

        InventoryChangeResult TryUnequipToContainer(
            int equipSlotIndex,
            string targetContainerId,
            int targetSlotIndex);

        InventoryChangeResult TryUnequipToFirstAvailable(
            int equipSlotIndex,
            string targetContainerId);

        InventoryChangeResult TrySwapEquippedSlots(int equipSlotA, int equipSlotB);
    }
}