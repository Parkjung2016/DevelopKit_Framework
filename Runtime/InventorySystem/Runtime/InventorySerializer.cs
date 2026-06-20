using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventorySerializer
    {
        public static InventoryContainerSaveData Export(InventoryContainer container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            return new InventoryContainerSaveData
            {
                Version = InventorySaveVersions.Current,
                ContainerId = container.ContainerId,
                Kind = container.Kind,
                Slots = container.ExportOccupiedSlots()
            };
        }

        public static InventoryGroupSaveData Export(InventoryGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            IReadOnlyList<InventoryContainer> containers = group.Containers;
            var saveData = new InventoryContainerSaveData[containers.Count];

            for (int i = 0; i < containers.Count; i++)
                saveData[i] = Export(containers[i]);

            return new InventoryGroupSaveData
            {
                Version = InventorySaveVersions.Current,
                Containers = saveData
            };
        }

        public static InventoryChangeResult Import(InventoryContainer container, InventoryContainerSaveData saveData) =>
            ImportWithReport(container, saveData, group: null).LastResult;

        public static InventoryImportReport ImportWithReport(
            InventoryContainer container,
            InventoryContainerSaveData saveData,
            InventoryGroup group = null,
            IItemDatabase validationDatabase = null)
        {
            var report = new InventoryImportReport();

            if (container == null)
                throw new ArgumentNullException(nameof(container));

            if (saveData == null)
            {
                report.LastResult = InventoryChangeResult.Fail(InventoryChangeType.Clear, InventoryFailReason.NoChange);
                return report;
            }

            InventorySaveMigrator.Migrate(ref saveData);
            container.ClearAll();

            IItemDatabase database = validationDatabase ?? group?.ItemDatabase;
            InventoryChangeResult lastResult = InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.NoChange);

            if (saveData.Slots == null)
            {
                report.LastResult = lastResult;
                return report;
            }

            foreach (InventorySlotSaveData slotSaveData in saveData.Slots)
            {
                if (database != null && !database.TryGetDefinition(slotSaveData.ItemId, out _))
                    report.AddWarning($"Unknown item id {slotSaveData.ItemId} in slot {slotSaveData.SlotIndex}.");

                if (slotSaveData.SlotIndex < 0 || slotSaveData.SlotIndex >= container.SlotCount)
                {
                    report.AddWarning($"Slot index {slotSaveData.SlotIndex} out of range for container {container.ContainerId}.");
                    continue;
                }

                lastResult = ImportSlot(container, slotSaveData);
            }

            report.LastResult = lastResult;
            return report;
        }

        public static void Import(InventoryGroup group, InventoryGroupSaveData saveData) =>
            ImportWithReport(group, saveData);

        public static InventoryImportReport ImportWithReport(InventoryGroup group, InventoryGroupSaveData saveData)
        {
            var report = new InventoryImportReport();

            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (saveData?.Containers == null)
            {
                report.LastResult = InventoryChangeResult.Fail(InventoryChangeType.Clear, InventoryFailReason.NoChange);
                return report;
            }

            InventorySaveMigrator.Migrate(ref saveData);

            InventoryChangeResult lastResult = InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.NoChange);
            foreach (InventoryContainerSaveData containerSaveData in saveData.Containers)
            {
                if (!group.TryGetContainer(containerSaveData.ContainerId, out InventoryContainer container))
                {
                    report.AddWarning($"Container {containerSaveData.ContainerId} not found during import.");
                    continue;
                }

                InventoryImportReport containerReport = ImportWithReport(container, containerSaveData, group, group.ItemDatabase);
                for (int i = 0; i < containerReport.Warnings.Count; i++)
                    report.AddWarning(containerReport.Warnings[i]);

                lastResult = containerReport.LastResult;
            }

            report.LastResult = lastResult;
            return report;
        }

        private static InventoryChangeResult ImportSlot(InventoryContainer container, InventorySlotSaveData slotSaveData)
        {
            if (slotSaveData.InstanceId > 0)
            {
                return container.TryAddItemToSlot(
                    slotSaveData.SlotIndex,
                    slotSaveData.ItemId,
                    slotSaveData.Count,
                    slotSaveData.InstanceId);
            }

            return container.TryAddItemToSlot(slotSaveData.SlotIndex, slotSaveData.ItemId, slotSaveData.Count);
        }
    }
}
