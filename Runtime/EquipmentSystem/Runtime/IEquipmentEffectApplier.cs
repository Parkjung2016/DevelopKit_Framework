using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public interface IEquipmentEffectApplier
    {
        void OnEquipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition);

        void OnUnequipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition);
    }

    public sealed class NullEquipmentEffectApplier : IEquipmentEffectApplier
    {
        public static readonly NullEquipmentEffectApplier Instance = new();

        public void OnEquipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition)
        {
        }

        public void OnUnequipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition)
        {
        }
    }
}
