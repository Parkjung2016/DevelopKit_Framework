namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [System.Serializable]
    public struct InventoryRecipeEntry
    {
        public int ItemId;
        public int Count;

        public InventoryRecipeEntry(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
