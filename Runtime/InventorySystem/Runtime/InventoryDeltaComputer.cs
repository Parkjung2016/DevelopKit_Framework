using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventoryDeltaComputer
    {
        public static InventoryContainerDelta ComputeContainerDelta(
            InventoryContainer container,
            InventoryContainerSaveData baseline)
        {
            if (container == null)
                return new InventoryContainerDelta();

            var changed = new List<InventorySlotDelta>();
            int slotCount = container.SlotCount;

            for (int i = 0; i < slotCount; i++)
            {
                container.TryGetSlot(i, out InventorySlot currentSlot);
                SlotData current = new SlotData
                {
                    ItemId = currentSlot.Stack.ItemId,
                    Count = currentSlot.Stack.Count,
                    InstanceId = currentSlot.InstanceId
                };

                SlotData baselineSlot = FindBaselineSlot(baseline, i);
                if (SlotsEqual(current, baselineSlot))
                    continue;

                changed.Add(InventorySlotDelta.FromChange(i, baselineSlot, current));
            }

            if (baseline?.Slots != null)
            {
                for (int i = 0; i < baseline.Slots.Length; i++)
                {
                    InventorySlotSaveData saveSlot = baseline.Slots[i];
                    if (saveSlot.SlotIndex >= slotCount)
                    {
                        SlotData before = SlotData.Create(saveSlot.ItemId, saveSlot.Count, saveSlot.InstanceId);
                        changed.Add(InventorySlotDelta.FromChange(saveSlot.SlotIndex, before, SlotData.Empty));
                    }
                }
            }

            return new InventoryContainerDelta
            {
                ContainerId = container.ContainerId,
                Revision = container.Revision,
                Slots = changed.ToArray()
            };
        }

        public static InventoryGroupDelta ComputeGroupDelta(
            InventoryGroup group,
            InventoryGroupSaveData baseline)
        {
            if (group == null)
                return new InventoryGroupDelta();

            IReadOnlyList<InventoryContainer> containers = group.Containers;
            var deltas = new InventoryContainerDelta[containers.Count];

            for (int i = 0; i < containers.Count; i++)
            {
                InventoryContainer container = containers[i];
                InventoryContainerSaveData containerBaseline = FindContainerBaseline(baseline, container.ContainerId);
                deltas[i] = ComputeContainerDelta(container, containerBaseline);
            }

            return new InventoryGroupDelta { Containers = deltas };
        }

        public static void ApplyContainerDelta(InventoryContainer container, InventoryContainerDelta delta)
        {
            if (container == null || delta?.Slots == null)
                return;

            for (int i = 0; i < delta.Slots.Length; i++)
            {
                InventorySlotDelta slotDelta = delta.Slots[i];
                if (slotDelta.CurrentItemId <= 0 || slotDelta.CurrentCount <= 0)
                {
                    container.ClearSlot(slotDelta.SlotIndex);
                    continue;
                }

                container.TryAddItemToSlot(
                    slotDelta.SlotIndex,
                    slotDelta.CurrentItemId,
                    slotDelta.CurrentCount,
                    slotDelta.CurrentInstanceId);
            }
        }

        private static InventoryContainerSaveData FindContainerBaseline(InventoryGroupSaveData baseline, string containerId)
        {
            if (baseline?.Containers == null)
                return null;

            for (int i = 0; i < baseline.Containers.Length; i++)
            {
                InventoryContainerSaveData container = baseline.Containers[i];
                if (container.ContainerId == containerId)
                    return container;
            }

            return null;
        }

        private static SlotData FindBaselineSlot(InventoryContainerSaveData baseline, int slotIndex)
        {
            if (baseline?.Slots == null)
                return SlotData.Empty;

            for (int i = 0; i < baseline.Slots.Length; i++)
            {
                InventorySlotSaveData saveSlot = baseline.Slots[i];
                if (saveSlot.SlotIndex == slotIndex)
                    return SlotData.Create(saveSlot.ItemId, saveSlot.Count, saveSlot.InstanceId);
            }

            return SlotData.Empty;
        }

        private static bool SlotsEqual(in SlotData a, in SlotData b) =>
            a.ItemId == b.ItemId && a.Count == b.Count && a.InstanceId == b.InstanceId;
    }
}
