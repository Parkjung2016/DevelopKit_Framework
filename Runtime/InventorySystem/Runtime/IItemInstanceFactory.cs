namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>고유 아이템 생성 시 기본 <see cref="IItemInstanceData"/>를 만듭니다.</summary>
    public interface IItemInstanceFactory
    {
        bool TryCreate(int itemId, out IItemInstanceData data);
    }

    internal sealed class NullItemInstanceFactory : IItemInstanceFactory
    {
        public static readonly NullItemInstanceFactory Instance = new();

        public bool TryCreate(int itemId, out IItemInstanceData data)
        {
            data = null;
            return false;
        }
    }
}
