namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct ItemDefinition
    {
        public int ItemId { get; }
        public ItemType ItemType { get; }
        /// <summary>실제 인벤토리 연산에 쓰이는 최대 스택 수. 비스택형은 1, 스택형은 1 이상.</summary>
        public int MaxStackSize { get; }
        public bool IsStackable { get; }
        public bool CanDrop { get; }
        public bool CanTrade { get; }
        public float Weight { get; }

        public bool RequiresUniqueInstance => !IsStackable;

        public static int ResolveMaxStackSize(int maxStackSize, bool isStackable) =>
            isStackable ? (maxStackSize > 0 ? maxStackSize : 1) : 1;

        public ItemDefinition(
            int itemId,
            int maxStackSize = 99,
            bool isStackable = true,
            ItemType itemType = default,
            bool canDrop = true,
            bool canTrade = true,
            float weight = 0f)
        {
            ItemId = itemId;
            IsStackable = isStackable;
            MaxStackSize = ResolveMaxStackSize(maxStackSize, isStackable);
            ItemType = itemType;
            CanDrop = canDrop;
            CanTrade = canTrade;
            Weight = weight;
        }
    }
}
