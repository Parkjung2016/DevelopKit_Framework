using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public interface IReadOnlyEquipment
    {
        event Action<EquipmentChangeEventArgs> OnEquipmentChanged;

        string EquipmentContainerId { get; }
        int SlotCount { get; }

        bool TryGetEquippedSlot(int equipSlotIndex, out InventorySlot slot);
        bool IsEquipped(int equipSlotIndex);
    }
}