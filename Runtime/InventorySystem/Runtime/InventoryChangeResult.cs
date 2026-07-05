using System;
using System.Text;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct InventoryChangeResult
    {
        public InventoryChangeType ChangeType { get; }
        public InventoryFailReason Reason { get; }
        public int ItemId { get; }
        public ItemDefinition Definition { get; }
        public ItemDefinition SecondaryDefinition { get; }
        public int RequestedCount { get; }
        public int ProcessedCount { get; }
        public int Remainder { get; }
        public int TotalItemCountBefore { get; }
        public int TotalItemCountAfter { get; }
        public int PrimarySlotIndex { get; }
        public int SecondarySlotIndex { get; }
        public bool ItemWasAcquired { get; }
        public bool ItemWasDepleted { get; }
        public int[] ChangedSlotIndices { get; }
        public InventorySlotChange[] SlotChanges { get; }
        public string ContainerId { get; }
        public ContainerKind Kind { get; }
        public string SecondaryContainerId { get; }

        public bool Success => Reason == InventoryFailReason.None &&
                               (ProcessedCount > 0 || ChangeType is InventoryChangeType.Move or InventoryChangeType.Swap);

        public bool IsPartialSuccess => Success && Remainder > 0;

        public bool HasDefinition => Definition.ItemId > 0;

        public bool HasSecondaryDefinition => SecondaryDefinition.ItemId > 0;

        public int TotalCountDelta => TotalItemCountAfter - TotalItemCountBefore;

        public InventoryChangeResult(
            InventoryChangeType changeType,
            InventoryFailReason reason,
            int itemId,
            ItemDefinition definition,
            ItemDefinition secondaryDefinition,
            int requestedCount,
            int processedCount,
            int remainder,
            int totalItemCountBefore,
            int totalItemCountAfter,
            int primarySlotIndex,
            int secondarySlotIndex,
            bool itemWasAcquired,
            bool itemWasDepleted,
            int[] changedSlotIndices,
            InventorySlotChange[] slotChanges,
            string containerId = "main",
            ContainerKind containerKind = (ContainerKind)InventoryEnumCore.MainContainerKindValue,
            string secondaryContainerId = null)
        {
            ChangeType = changeType;
            Reason = reason;
            ItemId = itemId;
            Definition = definition;
            SecondaryDefinition = secondaryDefinition;
            RequestedCount = requestedCount;
            ProcessedCount = processedCount;
            Remainder = remainder;
            TotalItemCountBefore = totalItemCountBefore;
            TotalItemCountAfter = totalItemCountAfter;
            PrimarySlotIndex = primarySlotIndex;
            SecondarySlotIndex = secondarySlotIndex;
            ItemWasAcquired = itemWasAcquired;
            ItemWasDepleted = itemWasDepleted;
            ChangedSlotIndices = changedSlotIndices ?? Array.Empty<int>();
            SlotChanges = slotChanges ?? Array.Empty<InventorySlotChange>();
            ContainerId = containerId ?? "main";
            Kind = containerKind;
            SecondaryContainerId = secondaryContainerId;
        }

        public static InventoryChangeResult Fail(
            InventoryChangeType changeType,
            InventoryFailReason reason,
            int itemId = 0,
            int requestedCount = 0,
            int primarySlotIndex = -1,
            int secondarySlotIndex = -1,
            int totalItemCountBefore = 0,
            ItemDefinition definition = default,
            ItemDefinition secondaryDefinition = default,
            string containerId = "main",
            ContainerKind containerKind = (ContainerKind)InventoryEnumCore.MainContainerKindValue) =>
            new(
                changeType,
                reason,
                itemId,
                definition,
                secondaryDefinition,
                requestedCount,
                0,
                requestedCount,
                totalItemCountBefore,
                totalItemCountBefore,
                primarySlotIndex,
                secondarySlotIndex,
                false,
                false,
                Array.Empty<int>(),
                Array.Empty<InventorySlotChange>(),
                containerId,
                containerKind);

        public static InventoryChangeResult Succeed(
            InventoryChangeType changeType,
            int itemId,
            ItemDefinition definition,
            ItemDefinition secondaryDefinition,
            int requestedCount,
            int processedCount,
            int remainder,
            int totalItemCountBefore,
            int totalItemCountAfter,
            int primarySlotIndex,
            int secondarySlotIndex,
            bool itemWasAcquired,
            bool itemWasDepleted,
            string containerId,
            ContainerKind containerKind,
            string secondaryContainerId,
            int[] changedSlotIndices,
            InventorySlotChange[] slotChanges) =>
            new(
                changeType,
                InventoryFailReason.None,
                itemId,
                definition,
                secondaryDefinition,
                requestedCount,
                processedCount,
                remainder,
                totalItemCountBefore,
                totalItemCountAfter,
                primarySlotIndex,
                secondarySlotIndex,
                itemWasAcquired,
                itemWasDepleted,
                changedSlotIndices,
                slotChanges,
                containerId,
                containerKind,
                secondaryContainerId);

        public InventoryChangeResult WithSecondaryContainer(string secondaryContainerId) =>
            new(
                ChangeType,
                Reason,
                ItemId,
                Definition,
                SecondaryDefinition,
                RequestedCount,
                ProcessedCount,
                Remainder,
                TotalItemCountBefore,
                TotalItemCountAfter,
                PrimarySlotIndex,
                SecondarySlotIndex,
                ItemWasAcquired,
                ItemWasDepleted,
                ChangedSlotIndices,
                SlotChanges,
                ContainerId,
                Kind,
                secondaryContainerId);

        public static InventoryChangeResult Succeed(
            InventoryChangeType changeType,
            int itemId,
            ItemDefinition definition,
            ItemDefinition secondaryDefinition,
            int requestedCount,
            int processedCount,
            int remainder,
            int totalItemCountBefore,
            int totalItemCountAfter,
            int primarySlotIndex,
            int secondarySlotIndex,
            bool itemWasAcquired,
            bool itemWasDepleted,
            int[] changedSlotIndices,
            InventorySlotChange[] slotChanges) =>
            Succeed(
                changeType,
                itemId,
                definition,
                secondaryDefinition,
                requestedCount,
                processedCount,
                remainder,
                totalItemCountBefore,
                totalItemCountAfter,
                primarySlotIndex,
                secondarySlotIndex,
                itemWasAcquired,
                itemWasDepleted,
                "main",
                (ContainerKind)InventoryEnumCore.MainContainerKindValue,
                null,
                changedSlotIndices,
                slotChanges);

        public override string ToString() => ToDebugJson();

        public string ToDebugJson(bool pretty = true)
        {
            if (!pretty)
            {
                return
                    $"{{\"Success\":{Bool(Success)},\"ChangeType\":\"{ChangeType}\",\"Reason\":\"{Reason}\",\"ItemId\":{ItemId},\"Definition\":{FormatDefinition(Definition,false)},\"SecondaryDefinition\":{FormatDefinition(SecondaryDefinition,false)},\"RequestedCount\":{RequestedCount},\"ProcessedCount\":{ProcessedCount},\"Remainder\":{Remainder},\"IsPartialSuccess\":{Bool(IsPartialSuccess)},\"TotalItemCountBefore\":{TotalItemCountBefore},\"TotalItemCountAfter\":{TotalItemCountAfter},\"TotalCountDelta\":{TotalCountDelta},\"PrimarySlotIndex\":{PrimarySlotIndex},\"SecondarySlotIndex\":{SecondarySlotIndex},\"ItemWasAcquired\":{Bool(ItemWasAcquired)},\"ItemWasDepleted\":{Bool(ItemWasDepleted)},\"ChangedSlotIndices\":{FormatIndices(ChangedSlotIndices)},\"SlotChanges\":{FormatSlotChanges(SlotChanges,false)}}}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"Success\": {Bool(Success)},");
            sb.AppendLine($"  \"ChangeType\": \"{ChangeType}\",");
            sb.AppendLine($"  \"Reason\": \"{Reason}\",");
            sb.AppendLine($"  \"ItemId\": {ItemId},");
            sb.AppendLine($"  \"Definition\": {FormatDefinition(Definition, true)},");
            sb.AppendLine($"  \"SecondaryDefinition\": {FormatDefinition(SecondaryDefinition, true)},");
            sb.AppendLine($"  \"RequestedCount\": {RequestedCount},");
            sb.AppendLine($"  \"ProcessedCount\": {ProcessedCount},");
            sb.AppendLine($"  \"Remainder\": {Remainder},");
            sb.AppendLine($"  \"IsPartialSuccess\": {Bool(IsPartialSuccess)},");
            sb.AppendLine($"  \"TotalItemCountBefore\": {TotalItemCountBefore},");
            sb.AppendLine($"  \"TotalItemCountAfter\": {TotalItemCountAfter},");
            sb.AppendLine($"  \"TotalCountDelta\": {TotalCountDelta},");
            sb.AppendLine($"  \"PrimarySlotIndex\": {PrimarySlotIndex},");
            sb.AppendLine($"  \"SecondarySlotIndex\": {SecondarySlotIndex},");
            sb.AppendLine($"  \"ContainerId\": \"{ContainerId}\",");
            sb.AppendLine($"  \"ContainerKind\": \"{Kind}\",");
            sb.AppendLine($"  \"SecondaryContainerId\": {(SecondaryContainerId == null ? "null" : $"\"{SecondaryContainerId}\"")},");
            sb.AppendLine($"  \"ItemWasAcquired\": {Bool(ItemWasAcquired)},");
            sb.AppendLine($"  \"ItemWasDepleted\": {Bool(ItemWasDepleted)},");
            sb.AppendLine($"  \"ChangedSlotIndices\": {FormatIndices(ChangedSlotIndices)},");
            sb.Append($"  \"SlotChanges\": {FormatSlotChanges(SlotChanges, true)}");
            sb.AppendLine();
            sb.Append('}');
            return sb.ToString();
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string FormatDefinition(ItemDefinition definition, bool pretty)
        {
            if (definition.ItemId <= 0)
                return "null";

            return pretty
                ? $"{{ \"ItemId\": {definition.ItemId}, \"ItemType\": \"{definition.ItemType}\", \"MaxStackSize\": {definition.MaxStackSize}, \"IsStackable\": {Bool(definition.IsStackable)}, \"CanDrop\": {Bool(definition.CanDrop)}, \"CanTrade\": {Bool(definition.CanTrade)} }}"
                : $"{{\"ItemId\":{definition.ItemId},\"ItemType\":\"{definition.ItemType}\",\"MaxStackSize\":{definition.MaxStackSize},\"IsStackable\":{Bool(definition.IsStackable)},\"CanDrop\":{Bool(definition.CanDrop)},\"CanTrade\":{Bool(definition.CanTrade)}}}";
        }

        private static string FormatIndices(int[] indices)
        {
            if (indices == null || indices.Length == 0)
                return "[]";

            var sb = new StringBuilder("[");
            for (int i = 0; i < indices.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                sb.Append(indices[i]);
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string FormatSlotChanges(InventorySlotChange[] changes, bool pretty)
        {
            if (changes == null || changes.Length == 0)
                return "[]";

            var sb = new StringBuilder("[");
            for (int i = 0; i < changes.Length; i++)
            {
                InventorySlotChange change = changes[i];
                if (i > 0)
                    sb.Append(pretty ? ",\n    " : ", ");

                if (pretty)
                {
                    sb.Append("{ ");
                    sb.Append($"\"SlotIndex\": {change.SlotIndex}, ");
                    sb.Append($"\"PreviousItemId\": {change.PreviousItemId}, ");
                    sb.Append($"\"PreviousCount\": {change.PreviousCount}, ");
                    sb.Append($"\"CurrentItemId\": {change.CurrentItemId}, ");
                    sb.Append($"\"CurrentCount\": {change.CurrentCount}, ");
                    sb.Append($"\"CountDelta\": {change.CountDelta} ");
                    sb.Append('}');
                }
                else
                {
                    sb.Append('{');
                    sb.Append($"\"SlotIndex\":{change.SlotIndex},");
                    sb.Append($"\"PreviousItemId\":{change.PreviousItemId},");
                    sb.Append($"\"PreviousCount\":{change.PreviousCount},");
                    sb.Append($"\"CurrentItemId\":{change.CurrentItemId},");
                    sb.Append($"\"CurrentCount\":{change.CurrentCount},");
                    sb.Append($"\"CountDelta\":{change.CountDelta}");
                    sb.Append('}');
                }
            }

            if (pretty && changes.Length > 0)
                sb.Append('\n');

            sb.Append(']');
            return sb.ToString();
        }
    }
}
