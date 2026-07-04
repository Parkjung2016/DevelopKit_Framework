using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary><see cref="IEquipmentVisualDataSource"/>를 <see cref="IEquipmentVisualResolver"/>에 연결합니다.</summary>
    public sealed class DataSourceEquipmentVisualResolver : IEquipmentVisualResolver
    {
        private readonly IEquipmentVisualDataSource dataSource;

        public DataSourceEquipmentVisualResolver(IEquipmentVisualDataSource dataSource)
        {
            this.dataSource = dataSource ?? NullEquipmentVisualDataSource.Instance;
        }

        public bool TryResolve(
            int equipSlotIndex,
            string slotCategory,
            in ItemStack stack,
            in ItemDefinition definition,
            out EquipmentVisualDefinition visual)
        {
            visual = default;

            if (!dataSource.TryGetByItemId(stack.ItemId, out EquipmentVisualRecord record))
                return false;

            visual = EquipmentVisualDefinition.FromModelKey(record.ModelKey);
            return !visual.IsEmpty;
        }
    }
}
