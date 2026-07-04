using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary><see cref="EquipmentVisualController"/>를 <see cref="IEquipmentEffectApplier"/>에 연결합니다.</summary>
    public sealed class EquipmentVisualEffectApplier : IEquipmentEffectApplier
    {
        private readonly EquipmentVisualController visualController;

        public EquipmentVisualEffectApplier(EquipmentVisualController visualController)
        {
            this.visualController = visualController;
        }

        public void OnEquipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition) =>
            visualController?.Equip(equipSlotIndex, stack, definition);

        public void OnUnequipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition) =>
            visualController?.Unequip(equipSlotIndex);
    }
}
