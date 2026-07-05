namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>슬롯 밖에 저장되는 아이템 인스턴스 payload입니다.</summary>
    public interface IItemInstanceData
    {
        int ItemId { get; }
        long InstanceId { get; }
        bool IsBound { get; }
    }
}
