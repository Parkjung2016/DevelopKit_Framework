using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 플레이어·UI용 인벤토리 진입점입니다. 단일/멀티 컨테이너와 제작·루팅을 모두 처리합니다.
    /// </summary>
    public partial class InventorySystem : MonoBehaviour, IInventoryContainer
    {
        public delegate void SlotChangeHandler(int slotIndex, InventorySlot slot);

        public event SlotChangeHandler OnSlotChanged;
        public event Action<InventoryChangeResult> OnInventoryChanged;
        public event Action<int> OnItemAcquired;
        public event Action<int> OnItemDepleted;


        [SerializeField] private InventorySetupSO setup;

        private InventoryGroup group;
        private InventoryContainer primaryContainer;
        private IInventoryOwner owner;

        public InventorySetupSO Setup => setup;
        public InventoryGroup Group => group;

        public string ContainerId => Primary.ContainerId;
        public ContainerKind Kind => Primary.Kind;
        public InventoryContainerDescriptor Descriptor => Primary.Descriptor;
        public int SlotCount => Primary.SlotCount;
        public int Revision => Primary.Revision;
        public InventoryContainer Container => Primary;

        private InventoryContainer Primary
        {
            get
            {
                EnsureInitialized();
                return primaryContainer;
            }
        }

        /// <summary>
        /// <see cref="InventorySetupSO"/>로 인벤토리를 초기화합니다. setupAsset을 생략하면 Inspector의 <see cref="setup"/>을 사용합니다.
        /// </summary>
        public void Init(IInventoryOwner owner, InventorySetupSO setupAsset = null, IItemContainerRouter router = null)
        {
            InventorySetupSO resolvedSetup = setupAsset ?? setup;
            if (resolvedSetup == null)
            {
                CDebug.LogWarning("InventorySystem : InventorySetupSO is required.");
                return;
            }

            setup = resolvedSetup;
            this.owner = owner;

            resolvedSetup.RegisterGlobalItemCatalog();
            IInventoryDataProvider dataProvider = resolvedSetup.CreateDataProvider();
            RebuildGroup(resolvedSetup.CreateContainerConfigs(), router, dataProvider);
        }

        public bool TryGetContainer(string containerId, out IInventoryContainer container)
        {
            container = null;
            if (group == null || string.IsNullOrEmpty(containerId))
                return false;

            if (!group.TryGetContainer(containerId, out InventoryContainer found))
                return false;

            container = found;
            return true;
        }

        public bool TryGetContainerByKind(ContainerKind kind, out IInventoryContainer container)
        {
            if (group != null && group.TryGetContainerByKind(kind, out container))
                return true;

            container = null;
            return false;
        }

        private void OnDestroy() => DisposeGroup();

        public InventoryChangeResult TryAddItem(int itemId, int count) =>
            ExecuteGroup(() => group.TryAddItem(itemId, count), itemId, count, InventoryChangeType.Add);

        public InventoryChangeResult TryAddItemToContainer(string containerId, int itemId, int count) =>
            ExecuteGroup(
                () => group.TryAddItemToContainer(containerId, itemId, count),
                itemId,
                count,
                InventoryChangeType.Add);

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count) =>
            ExecuteContainer(
                () => Primary.TryAddItemToSlot(slotIndex, itemId, count),
                itemId,
                count,
                InventoryChangeType.Add);

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count, long instanceId) =>
            ExecuteContainer(
                () => Primary.TryAddItemToSlot(slotIndex, itemId, count, instanceId),
                itemId,
                count,
                InventoryChangeType.Add);

        public InventoryChangeResult TryRemoveItem(int itemId, int count) =>
            ExecuteGroup(() => group.TryRemoveItem(itemId, count), itemId, count, InventoryChangeType.Remove);

        public InventoryChangeResult TryRemoveItemFromSlot(int slotIndex, int count) =>
            ExecuteContainer(() => Primary.TryRemoveItemFromSlot(slotIndex, count), 0, count, InventoryChangeType.Remove);

        public InventoryChangeResult TryMoveSlot(int fromSlotIndex, int toSlotIndex) =>
            ExecuteContainer(
                () => Primary.TryMoveSlot(fromSlotIndex, toSlotIndex),
                0,
                0,
                InventoryChangeType.Move);

        public InventoryChangeResult TrySwapSlots(int slotIndexA, int slotIndexB) =>
            ExecuteContainer(
                () => Primary.TrySwapSlots(slotIndexA, slotIndexB),
                0,
                0,
                InventoryChangeType.Swap);

        public InventoryChangeResult TryMoveBetween(
            string fromContainerId,
            int fromSlotIndex,
            string toContainerId,
            int toSlotIndex) =>
            ExecuteGroup(
                () => group.TryMoveBetween(fromContainerId, fromSlotIndex, toContainerId, toSlotIndex),
                0,
                0,
                InventoryChangeType.Move);

        public InventoryChangeResult TrySwapBetween(
            string containerAId,
            int slotA,
            string containerBId,
            int slotB) =>
            ExecuteGroup(
                () => group.TrySwapBetween(containerAId, slotA, containerBId, slotB),
                0,
                0,
                InventoryChangeType.Swap);

        public InventoryChangeResult ClearSlot(int slotIndex) =>
            ExecuteContainer(() => Primary.ClearSlot(slotIndex), 0, 0, InventoryChangeType.Clear);

        public InventoryChangeResult ClearAll() =>
            ExecuteContainer(() => Primary.ClearAll(), 0, 0, InventoryChangeType.Clear);

        public InventoryChangeResult TrySplitStack(int slotIndex, int splitCount) =>
            ExecuteContainer(
                () => Primary.TrySplitStack(slotIndex, splitCount),
                0,
                splitCount,
                InventoryChangeType.Split);

        public InventoryChangeResult TryDropItemFromSlot(int slotIndex, int count) =>
            ExecuteContainer(
                () => Primary.TryDropItemFromSlot(slotIndex, count),
                0,
                count,
                InventoryChangeType.Drop);

        public InventoryChangeResult TryTradeItemFromSlot(int slotIndex, int count) =>
            ExecuteContainer(
                () => Primary.TryTradeItemFromSlot(slotIndex, count),
                0,
                count,
                InventoryChangeType.Trade);

        public InventoryChangeResult TryUseItem(int slotIndex, IItemUseHandler handler) =>
            ExecuteContainer(() => Primary.TryUseItem(slotIndex, handler), 0, 0, InventoryChangeType.Use);

        public bool CanAddItem(int itemId, int count, out InventoryFailReason reason, out int addableCount)
        {
            if (group == null)
                return FailCanAddQuery(out reason, out addableCount, InventoryFailReason.DatabaseNotReady);

            if (!group.ItemDatabase.TryGetDefinition(itemId, out ItemDefinition definition))
                return FailCanAddQuery(out reason, out addableCount, InventoryFailReason.DefinitionNotFound);

            if (!group.TryGetContainerByKind((ContainerKind)InventoryEnumCore.MainContainerKindValue, out IInventoryContainer main))
                return Primary.CanAddItem(itemId, count, out reason, out addableCount);

            return main.CanAddItem(itemId, count, out reason, out addableCount);
        }

        public bool CanRemoveItem(int itemId, int count, out InventoryFailReason reason)
        {
            if (group == null)
                return FailQuery(out reason, InventoryFailReason.DatabaseNotReady);

            if (group.HasItem(itemId, count))
            {
                reason = InventoryFailReason.None;
                return true;
            }

            reason = InventoryFailReason.InsufficientItemCount;
            return false;
        }

        public bool CanMoveSlot(int fromSlotIndex, int toSlotIndex, out InventoryFailReason reason) =>
            Primary != null
                ? Primary.CanMoveSlot(fromSlotIndex, toSlotIndex, out reason)
                : FailQuery(out reason, InventoryFailReason.DatabaseNotReady);

        public bool CanSplitStack(int slotIndex, int splitCount, out InventoryFailReason reason, out int targetSlotIndex) =>
            Primary != null
                ? Primary.CanSplitStack(slotIndex, splitCount, out reason, out targetSlotIndex)
                : FailCanSplitQuery(out reason, out targetSlotIndex, InventoryFailReason.DatabaseNotReady);

        public bool HasItem(int itemId, int count = 1) =>
            group != null && group.HasItem(itemId, count);

        public int GetItemCount(int itemId) =>
            group?.GetItemCount(itemId) ?? 0;

        public int GetItemCount(string containerId, int itemId) =>
            group?.GetItemCount(containerId, itemId) ?? 0;

        public InventorySlot GetSlot(int slotIndex) =>
            Primary?.GetSlot(slotIndex) ?? default;

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (Primary == null)
            {
                CDebug.LogWarning("InventorySystem : not initialized.");
                slot = default;
                return false;
            }

            if (!Primary.TryGetSlot(slotIndex, out slot))
            {
                CDebug.LogWarning($"InventorySystem : invalid slot index {slotIndex}");
                return false;
            }

            return true;
        }

        public bool IsSlotEmpty(int slotIndex) =>
            Primary != null && Primary.IsSlotEmpty(slotIndex);

        public int GetFirstEmptySlotIndex() =>
            Primary?.GetFirstEmptySlotIndex() ?? -1;

        public int GetOccupiedSlotCount() =>
            Primary?.GetOccupiedSlotCount() ?? 0;

        /// <summary>InventoryGroup에서 발생한 변경 결과를 UI 이벤트로 전파합니다.</summary>
        public void NotifyChangeResult(InventoryChangeResult result) => ApplyChangeResult(result);

        public void FindSlotsWithItem(int itemId, List<int> results) =>
            Primary?.FindSlotsWithItem(itemId, results);

        public bool TryFindStackableSlot(int itemId, out int slotIndex)
        {
            if (Primary == null)
            {
                slotIndex = -1;
                return false;
            }

            return Primary.TryFindStackableSlot(itemId, out slotIndex);
        }

        public InventoryContainerSaveData ExportSaveData() =>
            Primary == null ? new InventoryContainerSaveData() : InventorySerializer.Export(Primary);

        public InventoryImportReport ImportSaveData(InventoryContainerSaveData saveData)
        {
            if (Primary == null)
            {
                CDebug.LogWarning("InventorySystem : not initialized.");
                return new InventoryImportReport
                {
                    LastResult = InventoryChangeResult.Fail(InventoryChangeType.Clear, InventoryFailReason.DatabaseNotReady)
                };
            }

            InventoryImportReport report = InventorySerializer.ImportWithReport(Primary, saveData);
            if (report.LastResult.Success)
                ApplyChangeResult(report.LastResult);

            return report;
        }

        public float GetTotalWeight() => Primary?.GetTotalWeight() ?? 0f;

        private void RebuildGroup(
            IReadOnlyList<InventoryContainerConfig> containerConfigs,
            IItemContainerRouter router,
            IInventoryDataProvider dataProvider = null)
        {
            DisposeGroup();
            group = new InventoryGroup(itemDatabase: null, router);

            if (dataProvider != null)
                group.SetDataServices(dataProvider.RecipeDatabase, dataProvider.LootTableDatabase);

            IReadOnlyList<InventoryContainerConfig> configs = containerConfigs;
            if (configs == null || configs.Count == 0)
            {
                if (setup != null)
                    configs = setup.CreateContainerConfigs();
            }

            if (configs == null || configs.Count == 0)
            {
                CDebug.LogWarning("InventorySystem : no container configs. Using default main container.");
                InventorySessionBuilder.RegisterContainers(group, Array.Empty<InventoryContainerConfig>(), out primaryContainer);
                return;
            }

            InventorySessionBuilder.RegisterContainers(group, configs, out primaryContainer);
        }

        private void DisposeGroup()
        {
            group?.Dispose();
            group = null;
            primaryContainer = null;
        }

        private void EnsureInitialized()
        {
            if (group != null)
                return;

            if (setup != null)
            {
                Init(owner);
                return;
            }

            CDebug.LogWarning("InventorySystem : not initialized.");
        }

        private InventoryChangeResult ExecuteContainer(
            Func<InventoryChangeResult> action,
            int itemId,
            int count,
            InventoryChangeType changeType)
        {
            if (Primary == null)
            {
                CDebug.LogWarning("InventorySystem : not initialized.");
                return InventoryChangeResult.Fail(changeType, InventoryFailReason.DatabaseNotReady, itemId, count);
            }

            InventoryChangeResult result = action();
            ApplyChangeResult(result);
            return result;
        }

        private InventoryChangeResult ExecuteGroup(
            Func<InventoryChangeResult> action,
            int itemId,
            int count,
            InventoryChangeType changeType)
        {
            if (group == null)
            {
                CDebug.LogWarning("InventorySystem : not initialized.");
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

            bool notifyPrimarySlots = primaryContainer != null
                && string.Equals(result.ContainerId, primaryContainer.ContainerId, StringComparison.Ordinal);

            InventorySlotChange[] slotChanges = result.SlotChanges;
            for (int i = 0; i < slotChanges.Length; i++)
            {
                InventorySlotChange change = slotChanges[i];
                if (notifyPrimarySlots)
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
