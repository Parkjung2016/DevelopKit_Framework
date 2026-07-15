using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Equipment System")]
    [DisallowMultipleComponent]
    public class ObjectEquipmentSystem : MonoBehaviour
    {
        [SerializeField] private EquipmentSetupSO setup;

        private EquipmentSystem equipment;
        private ObjectInventorySystem inventorySystem;
        private IEquipmentOwner owner;

        public EquipmentSetupSO Setup => setup;
        public EquipmentSystem Equipment => equipment;
        public IReadOnlyEquipment ReadOnlyEquipment => equipment;
        public IEquipmentOwner Owner => owner;
        public bool IsInitialized => equipment != null;

        public event Action<EquipmentChangeEventArgs> OnEquipmentChanged;

        public void Init(
            IEquipmentOwner owner,
            ObjectInventorySystem inventory,
            EquipmentSetupSO setupAsset = null,
            IEquipmentEffectApplier effectApplier = null)
        {
            ReleaseRuntime();

            ObjectInventorySystem candidateInventory = inventory;
            EquipmentSetupSO candidateSetup = setupAsset ?? setup;

            if (candidateInventory?.Group == null)
            {
                CDebug.LogWarning("ObjectEquipmentSystem : initialized ObjectInventorySystem is required.");
                return;
            }

            if (candidateSetup == null)
            {
                CDebug.LogWarning("ObjectEquipmentSystem : EquipmentSetupSO is required.");
                return;
            }

            if (!candidateInventory.Group.TryGetContainer(candidateSetup.ContainerId, out _))
            {
                CDebug.LogWarning(
                    $"ObjectEquipmentSystem : equipment container '{candidateSetup.ContainerId}' was not found.");
                return;
            }

            var candidateEquipment = new EquipmentSystem(
                candidateInventory.Group,
                candidateSetup,
                effectApplier);
            candidateEquipment.OnEquipmentChanged += HandleEquipmentChanged;

            this.owner = owner;
            inventorySystem = candidateInventory;
            setup = candidateSetup;
            equipment = candidateEquipment;
        }

        public void Clear() => ReleaseRuntime();

        public InventoryChangeResult TryEquipFromInventory(int inventorySlotIndex, int equipSlotIndex)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out ObjectInventorySystem inventory))
                return CreateNotReadyResult(InventoryChangeType.Move);

            return Complete(service.TryEquipFromContainer(
                inventory.ContainerId,
                inventorySlotIndex,
                equipSlotIndex));
        }

        public InventoryChangeResult TryUnequipToInventory(int equipSlotIndex, int inventorySlotIndex)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out ObjectInventorySystem inventory))
                return CreateNotReadyResult(InventoryChangeType.Move);

            return Complete(service.TryUnequipToContainer(
                equipSlotIndex,
                inventory.ContainerId,
                inventorySlotIndex));
        }

        public InventoryChangeResult TryUnequipToFirstInventorySlot(int equipSlotIndex)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out ObjectInventorySystem inventory))
                return CreateNotReadyResult(InventoryChangeType.Move);

            return Complete(service.TryUnequipToFirstAvailable(
                equipSlotIndex,
                inventory.ContainerId));
        }

        public InventoryChangeResult TrySwapEquippedSlots(int equipSlotA, int equipSlotB)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out _))
                return CreateNotReadyResult(InventoryChangeType.Swap);

            return Complete(service.TrySwapEquippedSlots(equipSlotA, equipSlotB));
        }

        private InventoryChangeResult Complete(InventoryChangeResult result)
        {
            if (result.Success)
                inventorySystem.NotifyChangeResult(result);

            return result;
        }

        private bool TryGetRuntime(
            out EquipmentSystem service,
            out ObjectInventorySystem inventory)
        {
            service = equipment;
            inventory = inventorySystem;
            return service != null && inventory != null;
        }

        private static InventoryChangeResult CreateNotReadyResult(InventoryChangeType changeType)
        {
            CDebug.LogWarning("ObjectEquipmentSystem : not initialized.");
            return InventoryChangeResult.Fail(changeType, InventoryFailReason.DatabaseNotReady);
        }

        private void HandleEquipmentChanged(EquipmentChangeEventArgs args) =>
            OnEquipmentChanged?.Invoke(args);

        private void ReleaseRuntime()
        {
            if (equipment != null)
                equipment.OnEquipmentChanged -= HandleEquipmentChanged;

            equipment = null;
            inventorySystem = null;
            owner = null;
        }

        private void OnDestroy() => ReleaseRuntime();
    }
}