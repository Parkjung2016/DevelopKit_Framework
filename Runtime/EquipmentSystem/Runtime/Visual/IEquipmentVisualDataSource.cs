namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>DT_ 테이블 등에서 읽어온 장비 비주얼 1행입니다.</summary>
    public struct EquipmentVisualRecord
    {
        public string ModelKey;

        public readonly bool IsEmpty => string.IsNullOrEmpty(ModelKey);
    }

    /// <summary>ItemId → <see cref="EquipmentVisualRecord"/> 조회. DT_ 테이블·SO·코드에서 구현합니다.</summary>
    public interface IEquipmentVisualDataSource
    {
        bool TryGetByItemId(int itemId, out EquipmentVisualRecord record);
    }

    public sealed class NullEquipmentVisualDataSource : IEquipmentVisualDataSource
    {
        public static readonly NullEquipmentVisualDataSource Instance = new();

        public bool TryGetByItemId(int itemId, out EquipmentVisualRecord record)
        {
            record = default;
            return false;
        }
    }
}
