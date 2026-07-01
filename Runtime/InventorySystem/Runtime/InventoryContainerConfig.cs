namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// ScriptableObject 없이 컨테이너를 구성할 때 사용하는 순수 런타임 설정입니다.
    /// </summary>
    public readonly struct InventoryContainerConfig
    {
        public int SlotCount { get; }
        public InventoryContainerDescriptor Descriptor { get; }

        public InventoryContainerConfig(int slotCount, InventoryContainerDescriptor descriptor)
        {
            SlotCount = slotCount;
            Descriptor = descriptor;
        }
    }
}
