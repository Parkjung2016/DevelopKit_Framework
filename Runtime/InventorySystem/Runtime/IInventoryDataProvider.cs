namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 인벤토리에서 사용할 아이템, 제작법, 보상 데이터를 제공합니다.
    /// SO뿐 아니라 테이블이나 서버 데이터도 이 인터페이스로 연결할 수 있습니다.
    /// </summary>
    public interface IInventoryDataProvider
    {
        IItemDatabase ItemDatabase { get; }
        IRecipeDatabase RecipeDatabase { get; }
        ILootTableDatabase LootTableDatabase { get; }
    }
}