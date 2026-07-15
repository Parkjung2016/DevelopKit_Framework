namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 인벤토리 조회와 상태 변경을 모두 제공하는 기본 컨테이너 계약입니다.
    /// </summary>
    public interface IInventoryContainer : IReadOnlyInventoryContainer, IInventoryContainerCommands
    {
    }
}
