using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed partial class InventoryGroup
    {
        private static InventoryChangeResult MergeRemoveResults(
            InventoryChangeResult first,
            InventoryChangeResult second,
            int requestedCount,
            int totalBefore) =>
            InventoryChangeResult.Succeed(
                InventoryChangeType.Remove,
                first.ItemId,
                first.Definition,
                default,
                requestedCount,
                first.ProcessedCount + second.ProcessedCount,
                0,
                totalBefore,
                totalBefore - first.ProcessedCount - second.ProcessedCount,
                first.PrimarySlotIndex,
                second.PrimarySlotIndex,
                false,
                false,
                first.ContainerId,
                first.Kind,
                second.ContainerId,
                MergeIndices(first.ChangedSlotIndices, second.ChangedSlotIndices),
                MergeSlotChanges(first.SlotChanges, second.SlotChanges));

        private static InventoryChangeResult NormalizeRemoveResult(
            InventoryChangeResult result,
            int requestedCount,
            int remainder,
            int totalBefore) =>
            InventoryChangeResult.Succeed(
                InventoryChangeType.Remove,
                result.ItemId,
                result.Definition,
                default,
                requestedCount,
                requestedCount - remainder,
                remainder,
                totalBefore,
                totalBefore - requestedCount + remainder,
                result.PrimarySlotIndex,
                result.SecondarySlotIndex,
                false,
                totalBefore == requestedCount - remainder,
                result.ContainerId,
                result.Kind,
                result.SecondaryContainerId,
                result.ChangedSlotIndices,
                result.SlotChanges);
        private static InventoryChangeResult MergeAddResults(InventoryChangeResult primary, InventoryChangeResult secondary)
        {
            if (!secondary.Success)
                return primary;

            return InventoryChangeResult.Succeed(
                InventoryChangeType.Add,
                secondary.ItemId,
                secondary.Definition,
                secondary.SecondaryDefinition,
                primary.RequestedCount,
                primary.ProcessedCount + secondary.ProcessedCount,
                secondary.Remainder,
                primary.TotalItemCountBefore,
                secondary.TotalItemCountAfter,
                secondary.PrimarySlotIndex,
                secondary.SecondarySlotIndex,
                primary.ItemWasAcquired || secondary.ItemWasAcquired,
                false,
                primary.ContainerId,
                primary.Kind,
                secondary.ContainerId,
                MergeIndices(primary.ChangedSlotIndices, secondary.ChangedSlotIndices),
                MergeSlotChanges(primary.SlotChanges, secondary.SlotChanges));
        }

        private static int[] MergeIndices(int[] a, int[] b)
        {
            if (a == null || a.Length == 0)
                return b ?? Array.Empty<int>();

            if (b == null || b.Length == 0)
                return a;

            var merged = new int[a.Length + b.Length];
            Array.Copy(a, merged, a.Length);
            Array.Copy(b, 0, merged, a.Length, b.Length);
            return merged;
        }

        private static InventorySlotChange[] MergeSlotChanges(InventorySlotChange[] a, InventorySlotChange[] b)
        {
            if (a == null || a.Length == 0)
                return b ?? Array.Empty<InventorySlotChange>();

            if (b == null || b.Length == 0)
                return a;

            var merged = new InventorySlotChange[a.Length + b.Length];
            Array.Copy(a, merged, a.Length);
            Array.Copy(b, 0, merged, a.Length, b.Length);
            return merged;
        }
    }
}
