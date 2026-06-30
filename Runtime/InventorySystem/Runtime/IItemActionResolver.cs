namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IItemActionResolver
    {
        bool TryResolve(int itemId, in ItemDefinition definition, out IItemUseHandler handler);
    }
}
