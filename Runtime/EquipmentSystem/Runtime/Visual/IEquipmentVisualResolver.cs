using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>아이템 → 장비 비주얼 정의 변환입니다. 게임 데이터(테이블, SO, 태그 등)는 이 인터페이스 구현체에 둡니다.</summary>
    public interface IEquipmentVisualResolver
    {
        bool TryResolve(
            int equipSlotIndex,
            string slotCategory,
            in ItemStack stack,
            in ItemDefinition definition,
            out EquipmentVisualDefinition visual);
    }

    public sealed class NullEquipmentVisualResolver : IEquipmentVisualResolver
    {
        public static readonly NullEquipmentVisualResolver Instance = new();

        public bool TryResolve(
            int equipSlotIndex,
            string slotCategory,
            in ItemStack stack,
            in ItemDefinition definition,
            out EquipmentVisualDefinition visual)
        {
            visual = default;
            return false;
        }
    }
}
