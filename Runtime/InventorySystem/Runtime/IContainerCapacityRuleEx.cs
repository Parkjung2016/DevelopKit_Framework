namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IContainerCapacityRuleEx : IContainerCapacityRule
    {
        bool CanAdd(InventoryContainer container, in ItemDefinition definition, int count);
    }
}
