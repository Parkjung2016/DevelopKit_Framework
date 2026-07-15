using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventoryQueries
    {
        public static float GetTotalWeight(IReadOnlyInventoryContainer container, IItemDatabase database)
        {
            if (container is InventoryContainer concrete)
                return concrete.GetTotalWeight();

            if (container == null || database == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < container.SlotCount; i++)
            {
                if (!container.TryGetSlot(i, out InventorySlot slot) || slot.IsEmpty)
                    continue;

                if (!database.TryGetDefinition(slot.Stack.ItemId, out ItemDefinition definition))
                    continue;

                total += definition.Weight * slot.Stack.Count;
            }

            return total;
        }

        public static int CountByType(IReadOnlyInventoryContainer container, IItemDatabase database, ItemType itemType)
        {
            if (container == null || database == null)
                return 0;

            int total = 0;
            for (int i = 0; i < container.SlotCount; i++)
            {
                if (!container.TryGetSlot(i, out InventorySlot slot) || slot.IsEmpty)
                    continue;

                if (!database.TryGetDefinition(slot.Stack.ItemId, out ItemDefinition definition))
                    continue;

                if (definition.ItemType == itemType)
                    total += slot.Stack.Count;
            }

            return total;
        }

        public static int CountOccupiedSlots(IReadOnlyInventoryContainer container) =>
            container?.GetOccupiedSlotCount() ?? 0;

        public static void CollectItemIds(IReadOnlyInventoryContainer container, HashSet<int> itemIds)
        {
            itemIds.Clear();
            if (container == null)
                return;

            for (int i = 0; i < container.SlotCount; i++)
            {
                if (!container.TryGetSlot(i, out InventorySlot slot) || slot.IsEmpty)
                    continue;

                itemIds.Add(slot.Stack.ItemId);
            }
        }

        public static bool HasAnyByType(IReadOnlyInventoryContainer container, IItemDatabase database, ItemType itemType) =>
            CountByType(container, database, itemType) > 0;
    }
}
