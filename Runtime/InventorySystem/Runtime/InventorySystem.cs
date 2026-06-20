using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;
namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public class InventorySystem : MonoBehaviour, IInventoryContainer
    {
        public delegate void SlotChangeHandler(int slotIndex, InventorySlot slot);

        public event SlotChangeHandler OnSlotChanged;
        public event Action<InventoryChangeResult> OnInventoryChanged;
        public event Action<int> OnItemAcquired;
        public event Action<int> OnItemDepleted;

        [field: SerializeField] public InventoryConfigSO inventoryConfig { get; private set; }

        private InventoryContainer container;

        public string ContainerId => container?.ContainerId ?? inventoryConfig?.ContainerId ?? "main";
        public ContainerKind Kind => container?.Kind ?? inventoryConfig?.Kind ?? ContainerKind.Main;
        public InventoryContainerDescriptor Descriptor => container?.Descriptor ?? inventoryConfig?.CreateDescriptor() ?? InventoryContainerDescriptor.Main();
        public int SlotCount => container?.SlotCount ?? 0;
        public int Revision => container?.Revision ?? 0;
        public InventoryContainer Container => container;

        public void Init(IInventoryOwner owner, InventorySetupSO setup)
        {
            if (setup == null)
            {
                CDebug.LogWarning("InventorySetupSO is null.");
                return;
            }

            Init(owner, setup.ItemDatabase);
        }

        public void Init(IInventoryOwner owner, IItemDatabase itemDatabase)
        {
            container?.Dispose();
            if (inventoryConfig == null)
            {
                CDebug.LogWarning("InventoryConfigSO is not assigned.");
                container = new InventoryContainer(0, itemDatabase);
                return;
            }

            container = new InventoryContainer(
                inventoryConfig.SlotCount,
                itemDatabase,
                inventoryConfig.CreateDescriptor());
        }

        private void OnDestroy() => container?.Dispose();

        public InventoryChangeResult TryAddItem(int itemId, int count) =>
            Execute(() => container.TryAddItem(itemId, count), itemId, count, InventoryChangeType.Add);

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count) =>
            Execute(() => container.TryAddItemToSlot(slotIndex, itemId, count), itemId, count, InventoryChangeType.Add);

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count, long instanceId) =>
            Execute(() => container.TryAddItemToSlot(slotIndex, itemId, count, instanceId), itemId, count, InventoryChangeType.Add);

        public InventoryChangeResult TryRemoveItem(int itemId, int count) =>
            Execute(() => container.TryRemoveItem(itemId, count), itemId, count, InventoryChangeType.Remove);

        public InventoryChangeResult TryRemoveItemFromSlot(int slotIndex, int count) =>
            Execute(() => container.TryRemoveItemFromSlot(slotIndex, count), 0, count, InventoryChangeType.Remove);

        public InventoryChangeResult TryMoveSlot(int fromSlotIndex, int toSlotIndex) =>
            Execute(() => container.TryMoveSlot(fromSlotIndex, toSlotIndex), 0, 0, InventoryChangeType.Move);

        public InventoryChangeResult TrySwapSlots(int slotIndexA, int slotIndexB) =>
            Execute(() => container.TrySwapSlots(slotIndexA, slotIndexB), 0, 0, InventoryChangeType.Swap);

        public InventoryChangeResult ClearSlot(int slotIndex) =>
            Execute(() => container.ClearSlot(slotIndex), 0, 0, InventoryChangeType.Clear);

        public InventoryChangeResult ClearAll() =>
            Execute(() => container.ClearAll(), 0, 0, InventoryChangeType.Clear);

        public InventoryChangeResult TrySplitStack(int slotIndex, int splitCount) =>
            Execute(() => container.TrySplitStack(slotIndex, splitCount), 0, splitCount, InventoryChangeType.Split);

        public InventoryChangeResult TryDropItemFromSlot(int slotIndex, int count) =>
            Execute(() => container.TryDropItemFromSlot(slotIndex, count), 0, count, InventoryChangeType.Drop);

        public InventoryChangeResult TryTradeItemFromSlot(int slotIndex, int count) =>
            Execute(() => container.TryTradeItemFromSlot(slotIndex, count), 0, count, InventoryChangeType.Trade);

        public InventoryChangeResult TryUseItem(int slotIndex, IItemUseHandler handler) =>
            Execute(() => container.TryUseItem(slotIndex, handler), 0, 0, InventoryChangeType.Use);

        public bool CanAddItem(int itemId, int count, out InventoryFailReason reason, out int addableCount) =>
            container != null
                ? container.CanAddItem(itemId, count, out reason, out addableCount)
                : FailCanAddQuery(out reason, out addableCount, InventoryFailReason.DatabaseNotReady);

        public bool CanRemoveItem(int itemId, int count, out InventoryFailReason reason) =>
            container != null
                ? container.CanRemoveItem(itemId, count, out reason)
                : FailQuery(out reason, InventoryFailReason.DatabaseNotReady);

        public bool CanMoveSlot(int fromSlotIndex, int toSlotIndex, out InventoryFailReason reason) =>
            container != null
                ? container.CanMoveSlot(fromSlotIndex, toSlotIndex, out reason)
                : FailQuery(out reason, InventoryFailReason.DatabaseNotReady);

        public bool CanSplitStack(int slotIndex, int splitCount, out InventoryFailReason reason, out int targetSlotIndex) =>
            container != null
                ? container.CanSplitStack(slotIndex, splitCount, out reason, out targetSlotIndex)
                : FailCanSplitQuery(out reason, out targetSlotIndex, InventoryFailReason.DatabaseNotReady);

        public bool HasItem(int itemId, int count = 1) =>
            container != null && container.HasItem(itemId, count);

        public int GetItemCount(int itemId) =>
            container?.GetItemCount(itemId) ?? 0;

        public InventorySlot GetSlot(int slotIndex) =>
            container?.GetSlot(slotIndex) ?? default;

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (container == null)
            {
                CDebug.LogWarning("not initialized.");
                slot = default;
                return false;
            }

            if (!container.TryGetSlot(slotIndex, out slot))
            {
                CDebug.LogWarning($"invalid slot index {slotIndex}");
                return false;
            }

            return true;
        }

        public bool IsSlotEmpty(int slotIndex) =>
            container != null && container.IsSlotEmpty(slotIndex);

        public int GetFirstEmptySlotIndex() =>
            container?.GetFirstEmptySlotIndex() ?? -1;

        public int GetOccupiedSlotCount() =>
            container?.GetOccupiedSlotCount() ?? 0;

        public void FindSlotsWithItem(int itemId, List<int> results) =>
            container?.FindSlotsWithItem(itemId, results);

        public bool TryFindStackableSlot(int itemId, out int slotIndex)
        {
            if (container == null)
            {
                slotIndex = -1;
                return false;
            }

            return container.TryFindStackableSlot(itemId, out slotIndex);
        }

        public InventoryContainerSaveData ExportSaveData() =>
            container == null ? new InventoryContainerSaveData() : InventorySerializer.Export(container);

        public InventoryImportReport ImportSaveData(InventoryContainerSaveData saveData)
        {
            if (container == null)
            {
                CDebug.LogWarning("not initialized.");
                return new InventoryImportReport
                {
                    LastResult = InventoryChangeResult.Fail(InventoryChangeType.Clear, InventoryFailReason.DatabaseNotReady)
                };
            }

            InventoryImportReport report = InventorySerializer.ImportWithReport(container, saveData);
            if (report.LastResult.Success)
                ApplyChangeResult(report.LastResult);

            return report;
        }

        public float GetTotalWeight() => container?.GetTotalWeight() ?? 0f;

        private InventoryChangeResult Execute(
            Func<InventoryChangeResult> action,
            int itemId,
            int count,
            InventoryChangeType changeType)
        {
            if (container == null)
            {
                CDebug.LogWarning("not initialized.");
                return InventoryChangeResult.Fail(changeType, InventoryFailReason.DatabaseNotReady, itemId, count);
            }

            InventoryChangeResult result = action();
            ApplyChangeResult(result);
            return result;
        }

        private void ApplyChangeResult(InventoryChangeResult result)
        {
            if (!result.Success)
                return;

            InventorySlotChange[] slotChanges = result.SlotChanges;
            for (int i = 0; i < slotChanges.Length; i++)
            {
                InventorySlotChange change = slotChanges[i];
                OnSlotChanged?.Invoke(change.SlotIndex, change.ToCurrentSlot());
            }

            if (result.ItemWasAcquired)
                OnItemAcquired?.Invoke(result.ItemId);

            if (result.ItemWasDepleted)
                OnItemDepleted?.Invoke(result.ItemId);

            OnInventoryChanged?.Invoke(result);
        }

        private static bool FailQuery(out InventoryFailReason reason, InventoryFailReason failReason)
        {
            reason = failReason;
            return false;
        }

        private static bool FailCanAddQuery(out InventoryFailReason reason, out int addableCount, InventoryFailReason failReason)
        {
            reason = failReason;
            addableCount = 0;
            return false;
        }

        private static bool FailCanSplitQuery(out InventoryFailReason reason, out int targetSlotIndex, InventoryFailReason failReason)
        {
            reason = failReason;
            targetSlotIndex = -1;
            return false;
        }
    }
}
