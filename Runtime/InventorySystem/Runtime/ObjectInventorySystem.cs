using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using PJDev.DevelopKit.Framework.Shared.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 게임 오브젝트에서 여러 인벤토리 컨테이너를 관리하는 진입점입니다.
    /// 실제 인벤토리 규칙과 데이터는 <see cref="InventoryGroup"/>에 위임합니다.
    /// </summary>
    [AddComponentMenu("PJDev/Framework/Object Inventory System")]
    public partial class ObjectInventorySystem : MonoBehaviour, IInventoryContainer
    {
        public delegate void SlotChangeHandler(int slotIndex, InventorySlot slot);

        public event SlotChangeHandler OnSlotChanged;
        public event Action<InventoryChangeResult> OnInventoryChanged;
        public event Action<int> OnItemAcquired;
        public event Action<int> OnItemDepleted;


        [SerializeField] private InventorySetupSO setup;
        [SerializeField] private InventoryDatabaseSetupSO databaseSetup = null;

        private InventoryGroup group;
        private InventoryContainer primaryContainer;
        private IInventoryOwner owner;

        public InventorySetupSO Setup => setup;
        public InventoryDatabaseSetupSO DatabaseSetup => databaseSetup;
        public InventoryGroup Group => group;
        public IItemInstanceStore ItemInstanceStore => group?.ItemInstanceStore;

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
        /// 설정 에셋을 사용해 인벤토리 컨테이너와 런타임 서비스를 초기화합니다.
        /// </summary>
        public void Init(
            IInventoryOwner owner,
            InventorySetupSO setupAsset = null,
            IItemContainerRouter router = null,
            IItemInstanceFactory instanceFactory = null,
            IItemInstanceIdGenerator instanceIdGenerator = null,
            FrameworkInitOptions initOptions = null)
        {
            InventorySetupSO resolvedSetup = setupAsset ?? setup;
            if (resolvedSetup == null)
            {
                CDebug.LogWarning("ObjectInventorySystem : InventorySetupSO is required.");
                return;
            }

            FrameworkInitOptions resolvedInit = initOptions ?? FrameworkInitOptions.Default;

            setup = resolvedSetup;
            this.owner = owner;

            if (resolvedInit.RegisterGlobalCatalogs)
                databaseSetup?.RegisterGlobals();

            RebuildGroup(resolvedSetup.CreateContainerConfigs(), router);

            if (instanceFactory != null)
                group.ItemInstanceFactory = instanceFactory;

            if (instanceIdGenerator != null)
                group.InstanceIdGenerator = instanceIdGenerator;

            ItemInstanceCatalog.Configure(
                group.ItemInstanceStore,
                group.ItemInstanceFactory,
                group.InstanceIdGenerator);
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

        public InventoryChangeResult TryAddItem(int itemId, int count)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Add, itemId, count)
                : Complete(currentGroup.TryAddItem(itemId, count));
        }

        public InventoryChangeResult TryAddItemToContainer(string containerId, int itemId, int count)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Add, itemId, count)
                : Complete(currentGroup.TryAddItemToContainer(containerId, itemId, count));
        }

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Add, itemId, count)
                : Complete(container.TryAddItemToSlot(slotIndex, itemId, count));
        }

        public InventoryChangeResult TryAddItemToSlot(int slotIndex, int itemId, int count, long instanceId)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Add, itemId, count)
                : Complete(container.TryAddItemToSlot(slotIndex, itemId, count, instanceId));
        }

        public InventoryChangeResult TryRemoveItem(int itemId, int count)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Remove, itemId, count)
                : Complete(currentGroup.TryRemoveItem(itemId, count));
        }

        public InventoryChangeResult TryRemoveItemFromSlot(int slotIndex, int count)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Remove, count: count)
                : Complete(container.TryRemoveItemFromSlot(slotIndex, count));
        }

        public InventoryChangeResult TryMoveSlot(int fromSlotIndex, int toSlotIndex)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Move)
                : Complete(container.TryMoveSlot(fromSlotIndex, toSlotIndex));
        }

        public InventoryChangeResult TrySwapSlots(int slotIndexA, int slotIndexB)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Swap)
                : Complete(container.TrySwapSlots(slotIndexA, slotIndexB));
        }

        public InventoryChangeResult TryMoveBetween(
            string fromContainerId,
            int fromSlotIndex,
            string toContainerId,
            int toSlotIndex)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Move)
                : Complete(currentGroup.TryMoveBetween(fromContainerId, fromSlotIndex, toContainerId, toSlotIndex));
        }

        public InventoryChangeResult TrySwapBetween(
            string containerAId,
            int slotA,
            string containerBId,
            int slotB)
        {
            InventoryGroup currentGroup = group;
            return currentGroup == null
                ? CreateNotReadyResult(InventoryChangeType.Swap)
                : Complete(currentGroup.TrySwapBetween(containerAId, slotA, containerBId, slotB));
        }

        public InventoryChangeResult ClearSlot(int slotIndex)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Clear)
                : Complete(container.ClearSlot(slotIndex));
        }

        public InventoryChangeResult ClearAll()
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Clear)
                : Complete(container.ClearAll());
        }

        public InventoryChangeResult TrySplitStack(int slotIndex, int splitCount)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Split, count: splitCount)
                : Complete(container.TrySplitStack(slotIndex, splitCount));
        }

        public InventoryChangeResult TryDropItemFromSlot(int slotIndex, int count)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Drop, count: count)
                : Complete(container.TryDropItemFromSlot(slotIndex, count));
        }

        public InventoryChangeResult TryTradeItemFromSlot(int slotIndex, int count)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Trade, count: count)
                : Complete(container.TryTradeItemFromSlot(slotIndex, count));
        }

        public InventoryChangeResult TryUseItem(int slotIndex, IItemUseHandler handler)
        {
            InventoryContainer container = Primary;
            return container == null
                ? CreateNotReadyResult(InventoryChangeType.Use)
                : Complete(container.TryUseItem(slotIndex, handler));
        }
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
                CDebug.LogWarning("ObjectInventorySystem : not initialized.");
                slot = default;
                return false;
            }

            if (!Primary.TryGetSlot(slotIndex, out slot))
            {
                CDebug.LogWarning($"ObjectInventorySystem : invalid slot index {slotIndex}");
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

        /// <summary>그룹에서 발생한 변경 결과를 컴포넌트 이벤트로 전달합니다.</summary>
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
                CDebug.LogWarning("ObjectInventorySystem : not initialized.");
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
            IItemContainerRouter router)
        {
            DisposeGroup();
            group = new InventoryGroup(itemDatabase: null, router);

            IReadOnlyList<InventoryContainerConfig> configs = containerConfigs;
            if (configs == null || configs.Count == 0)
            {
                if (setup != null)
                    configs = setup.CreateContainerConfigs();
            }

            if (configs == null || configs.Count == 0)
            {
                CDebug.LogWarning("ObjectInventorySystem : no container configs. Using default main container.");
                InventorySessionBuilder.RegisterContainers(group, Array.Empty<InventoryContainerConfig>(), out primaryContainer);
                return;
            }

            InventorySessionBuilder.RegisterContainers(group, configs, out primaryContainer);
        }

        private void DisposeGroup()
        {
            if (group != null && ReferenceEquals(ItemInstanceCatalog.Current, group.ItemInstanceStore))
                ItemInstanceCatalog.Clear();

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

            CDebug.LogWarning("ObjectInventorySystem : not initialized.");
        }

        private InventoryChangeResult Complete(InventoryChangeResult result)
        {
            ApplyChangeResult(result);
            return result;
        }

        private static InventoryChangeResult CreateNotReadyResult(
            InventoryChangeType changeType,
            int itemId = 0,
            int count = 0)
        {
            CDebug.LogWarning("ObjectInventorySystem : not initialized.");
            return InventoryChangeResult.Fail(
                changeType,
                InventoryFailReason.DatabaseNotReady,
                itemId,
                count);
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
