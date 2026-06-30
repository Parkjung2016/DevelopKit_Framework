namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IItemInstanceStore
    {
        bool TryGet<T>(long instanceId, out T data) where T : class, IItemInstanceData;

        void Set<T>(long instanceId, T data) where T : class, IItemInstanceData;

        bool Remove(long instanceId);

        bool Contains(long instanceId);
    }
}
