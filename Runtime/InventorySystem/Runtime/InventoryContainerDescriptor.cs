namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct InventoryContainerDescriptor
    {
        public string ContainerId { get; }
        public ContainerKind Kind { get; }
        public ISlotRule SlotRule { get; }
        public IContainerCapacityRule CapacityRule { get; }

        public InventoryContainerDescriptor(
            string containerId,
            ContainerKind kind,
            ISlotRule slotRule = null,
            IContainerCapacityRule capacityRule = null)
        {
            ContainerId = string.IsNullOrWhiteSpace(containerId) ? "main" : containerId;
            Kind = kind;
            SlotRule = slotRule ?? AnySlotRule.Instance;
            CapacityRule = capacityRule;
        }

        public static InventoryContainerDescriptor Main(string containerId = "main", ISlotRule slotRule = null, IContainerCapacityRule capacityRule = null) =>
            new(containerId, (ContainerKind)InventoryEnumCore.MainContainerKindValue, slotRule, capacityRule);
    }
}
