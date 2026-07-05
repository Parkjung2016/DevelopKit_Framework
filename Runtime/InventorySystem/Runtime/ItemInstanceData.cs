namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary><see cref="IItemInstanceData"/> 공통 필드를 제공하는 기본 클래스입니다.</summary>
    public abstract class ItemInstanceData : IItemInstanceData
    {
        public int ItemId { get; private set; }
        public long InstanceId { get; private set; }
        public bool IsBound => ItemId > 0 && InstanceId > 0;

        public void Initialize(int itemId, long instanceId)
        {
            ItemId = itemId;
            InstanceId = instanceId;
        }

        internal static void BindIfSupported(IItemInstanceData data, int itemId, long instanceId)
        {
            if (data is ItemInstanceData instanceData)
                instanceData.Initialize(itemId, instanceId);
        }
    }
}
