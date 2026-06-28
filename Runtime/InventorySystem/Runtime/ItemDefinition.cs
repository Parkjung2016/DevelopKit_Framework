namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct ItemDefinition
    {
        public int ItemId { get; }
        public ItemType ItemType { get; }
        public int MaxStackSize { get; }
        public bool IsStackable { get; }
        public bool CanDrop { get; }
        public bool CanTrade { get; }
        public float Weight { get; }

        public bool RequiresUniqueInstance => !IsStackable;

        public int EffectiveMaxStackSize => IsStackable ? (MaxStackSize > 0 ? MaxStackSize : 1) : 1;

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
            MaxStackSize = maxStackSize;
            IsStackable = isStackable;
            ItemType = itemType;
            CanDrop = canDrop;
            CanTrade = canTrade;
            Weight = weight;
        }
    }
}
