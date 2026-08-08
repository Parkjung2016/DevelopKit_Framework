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
        [SerializeField] private ObjectInventorySystem inventory;
        [SerializeField] private EquipmentSetupSO setup;

        private EquipmentSystem equipment;

        public ObjectInventorySystem Inventory => inventory;
        public EquipmentSetupSO Setup => setup;
        public EquipmentSystem Equipment => equipment;
        public IReadOnlyEquipment ReadOnlyEquipment => equipment;
        public bool IsInitialized => equipment != null;

        public event Action<EquipmentChangeEventArgs> OnEquipmentChanged;

        /// <summary>Inventory와 장비 설정을 연결해 런타임 장비 시스템을 준비합니다.</summary>
        public void Initialize(
            ObjectInventorySystem inventorySystem = null,
            EquipmentSetupSO setupAsset = null,
            IEquipmentEffectApplier effectApplier = null)
        {
            ReleaseRuntime();

            ObjectInventorySystem resolvedInventory = inventorySystem ?? inventory;
            EquipmentSetupSO resolvedSetup = setupAsset ?? setup;

            if (resolvedInventory == null || !resolvedInventory.IsInitialized)
            {
                CDebug.LogWarning("ObjectEquipmentSystem : initialized ObjectInventorySystem is required.");
                return;
            }

            if (resolvedSetup == null)
            {
                CDebug.LogWarning("ObjectEquipmentSystem : EquipmentSetupSO is required.");
                return;
            }

            if (!resolvedInventory.Group.TryGetContainer(resolvedSetup.ContainerId, out _))
            {
                CDebug.LogWarning(
                    $"ObjectEquipmentSystem : equipment container '{resolvedSetup.ContainerId}' was not found.");
                return;
            }

            var service = new EquipmentSystem(
                resolvedInventory.Group,
                resolvedSetup,
                effectApplier);
            service.OnEquipmentChanged += HandleEquipmentChanged;

            inventory = resolvedInventory;
            setup = resolvedSetup;
            equipment = service;
        }

        public void Clear() => ReleaseRuntime();

        public InventoryChangeResult TryEquipFromInventory(int inventorySlotIndex, int equipSlotIndex)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out ObjectInventorySystem currentInventory))
                return CreateNotReadyResult(InventoryChangeType.Move);

            return Complete(service.TryEquipFromContainer(
                currentInventory.ContainerId,
                inventorySlotIndex,
                equipSlotIndex));
        }

        public InventoryChangeResult TryUnequipToInventory(int equipSlotIndex, int inventorySlotIndex)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out ObjectInventorySystem currentInventory))
                return CreateNotReadyResult(InventoryChangeType.Move);

            return Complete(service.TryUnequipToContainer(
                equipSlotIndex,
                currentInventory.ContainerId,
                inventorySlotIndex));
        }

        public InventoryChangeResult TryUnequipToFirstInventorySlot(int equipSlotIndex)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out ObjectInventorySystem currentInventory))
                return CreateNotReadyResult(InventoryChangeType.Move);

            return Complete(service.TryUnequipToFirstAvailable(
                equipSlotIndex,
                currentInventory.ContainerId));
        }

        public InventoryChangeResult TrySwapEquippedSlots(int equipSlotA, int equipSlotB)
        {
            if (!TryGetRuntime(out EquipmentSystem service, out _))
                return CreateNotReadyResult(InventoryChangeType.Swap);

            return Complete(service.TrySwapEquippedSlots(equipSlotA, equipSlotB));
        }

        private void Start()
        {
            if (inventory != null && setup != null && equipment == null)
                Initialize();
        }

        private InventoryChangeResult Complete(InventoryChangeResult result)
        {
            if (result.Success)
                inventory.NotifyChangeResult(result);

            return result;
        }

        private bool TryGetRuntime(
            out EquipmentSystem service,
            out ObjectInventorySystem currentInventory)
        {
            service = equipment;
            currentInventory = inventory;
            return service != null && currentInventory != null;
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
        }

        private void OnDestroy() => ReleaseRuntime();
    }
}