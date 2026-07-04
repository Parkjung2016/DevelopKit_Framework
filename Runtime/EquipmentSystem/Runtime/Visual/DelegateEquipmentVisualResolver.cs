using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary><see cref="EquipmentVisualResolveHandler"/>로 resolver를 구성합니다.</summary>
    public sealed class DelegateEquipmentVisualResolver : IEquipmentVisualResolver
    {
        private readonly EquipmentVisualResolveHandler OnResolve;

        public DelegateEquipmentVisualResolver(EquipmentVisualResolveHandler OnResolve)
        {
            this.OnResolve = OnResolve ?? throw new ArgumentNullException(nameof(OnResolve));
        }

        public bool TryResolve(
            int equipSlotIndex,
            string slotCategory,
            in ItemStack stack,
            in ItemDefinition definition,
            out EquipmentVisualDefinition visual)
        {
            visual = OnResolve(equipSlotIndex, slotCategory, stack, definition);
            return !visual.IsEmpty;
        }
    }
}
