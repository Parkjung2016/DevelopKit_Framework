using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public delegate void EquipmentVisualSpawnCompletedHandler(ISocketItem socketItem);

    public delegate void EquipmentVisualSpawnHandler(
        in EquipmentVisualSpawnRequest request,
        EquipmentVisualSpawnCompletedHandler OnSpawnCompleted);

    public delegate void EquipmentVisualReleaseHandler(ISocketItem socketItem);

    public delegate EquipmentVisualDefinition EquipmentVisualResolveHandler(
        int equipSlotIndex,
        string slotCategory,
        ItemStack stack,
        ItemDefinition definition);
}
