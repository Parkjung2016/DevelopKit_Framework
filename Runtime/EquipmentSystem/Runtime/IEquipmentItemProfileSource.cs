using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public interface IEquipmentItemProfileSource
    {
        bool TryGetSlotCategory(int itemId, in ItemDefinition definition, out string slotCategory);
    }
}
