namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class SlotCountCapacityRule : IContainerCapacityRuleEx
    {
        private readonly int maxOccupiedSlots;

        public int MaxOccupiedSlots => maxOccupiedSlots;

        public SlotCountCapacityRule(int maxOccupiedSlots) => this.maxOccupiedSlots = maxOccupiedSlots;

        public bool CanAdd(InventoryContainer container, in ItemDefinition definition, int count)
        {
            if (container == null)
                return false;

            if (container.GetOccupiedSlotCount() < maxOccupiedSlots)
                return true;

            return container.GetItemCount(definition.ItemId) > 0
                && container.SimulateAddWithoutCapacityCheck(definition, count) > 0;
        }

        public bool CanAdd(in ItemDefinition definition, int count, int currentItemCount, int occupiedSlotCount) =>
            occupiedSlotCount < maxOccupiedSlots || currentItemCount > 0;
    }
}
