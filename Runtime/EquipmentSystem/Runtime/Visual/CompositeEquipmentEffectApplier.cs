using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>스탯·비주얼 등 여러 <see cref="IEquipmentEffectApplier"/>를 한 번에 등록합니다.</summary>
    public sealed class CompositeEquipmentEffectApplier : IEquipmentEffectApplier
    {
        private readonly IEquipmentEffectApplier[] appliers;

        public CompositeEquipmentEffectApplier(params IEquipmentEffectApplier[] appliers)
        {
            this.appliers = appliers == null
                ? Array.Empty<IEquipmentEffectApplier>()
                : (IEquipmentEffectApplier[])appliers.Clone();
        }

        public void OnEquipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition)
        {
            for (int i = 0; i < appliers.Length; i++)
                appliers[i]?.OnEquipped(equipSlotIndex, stack, definition);
        }

        public void OnUnequipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition)
        {
            for (int i = 0; i < appliers.Length; i++)
                appliers[i]?.OnUnequipped(equipSlotIndex, stack, definition);
        }
    }
}
