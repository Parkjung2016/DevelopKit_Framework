namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IItemContainerRouter
    {
        bool TryResolveContainer(InventoryGroup group, in ItemDefinition definition, out IInventoryContainer container);
    }
}
