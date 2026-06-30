using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEngine;
using ObjectInventorySystem = PJDev.DevelopKit.Framework.InventorySystem.Runtime.InventorySystem;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public class ObjectEquipmentSystem : MonoBehaviour
    {
        [SerializeField] private EquipmentSetupSO setup;

        private EquipmentSystem equipment;
        private ObjectInventorySystem inventorySystem;
        private IEquipmentOwner owner;

        public EquipmentSetupSO Setup => setup;
        public EquipmentSystem Equipment => equipment;

        public event Action<EquipmentChangeEventArgs> OnEquipmentChanged;

        public void Init(
            IEquipmentOwner owner,
            ObjectInventorySystem inventory,
            EquipmentSetupSO setupAsset = null,
            IEquipmentEffectApplier effectApplier = null)
        {
            this.owner = owner;
            inventorySystem = inventory;
            setup = setupAsset ?? setup;

            if (inventorySystem == null)
            {
                CDebug.LogWarning("ObjectEquipmentSystem : InventorySystem is null.");
                return;
            }

            if (setup == null)
            {
                CDebug.LogWarning("ObjectEquipmentSystem : EquipmentSetupSO is null.");
                return;
            }

            if (equipment != null)
                equipment.OnEquipmentChanged -= HandleEquipmentChanged;

            equipment = new EquipmentSystem(inventorySystem.Group, setup, effectApplier);
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }

        public InventoryChangeResult TryEquipFromInventory(int inventorySlotIndex, int equipSlotIndex) =>
            Execute(() => equipment.TryEquipFromContainer(inventorySystem.ContainerId, inventorySlotIndex, equipSlotIndex));

        public InventoryChangeResult TryUnequipToInventory(int equipSlotIndex, int inventorySlotIndex) =>
            Execute(() => equipment.TryUnequipToContainer(equipSlotIndex, inventorySystem.ContainerId, inventorySlotIndex));

        public InventoryChangeResult TryUnequipToFirstInventorySlot(int equipSlotIndex) =>
            Execute(() => equipment.TryUnequipToFirstAvailable(equipSlotIndex, inventorySystem.ContainerId));

        public InventoryChangeResult TrySwapEquippedSlots(int equipSlotA, int equipSlotB) =>
            Execute(() => equipment.TrySwapEquippedSlots(equipSlotA, equipSlotB));

        private InventoryChangeResult Execute(Func<InventoryChangeResult> action)
        {
            if (equipment == null || inventorySystem == null)
            {
                CDebug.LogWarning("ObjectEquipmentSystem : not initialized.");
                return InventoryChangeResult.Fail(InventoryChangeType.Move, InventoryFailReason.DatabaseNotReady);
            }

            InventoryChangeResult result = action();
            if (result.Success)
                inventorySystem.NotifyChangeResult(result);

            return result;
        }

        private void HandleEquipmentChanged(EquipmentChangeEventArgs args) => OnEquipmentChanged?.Invoke(args);

        private void OnDestroy()
        {
            if (equipment != null)
                equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        }
    }
}
