using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public delegate void EquipmentVisualSpawnCompletedHandler(GameObject instance);

    public delegate void EquipmentVisualSpawnHandler(
        in EquipmentVisualSpawnRequest request,
        EquipmentVisualSpawnCompletedHandler OnSpawnCompleted);

    public delegate void EquipmentVisualReleaseHandler(GameObject instance);

    public delegate EquipmentVisualDefinition EquipmentVisualResolveHandler(
        int equipSlotIndex,
        string slotCategory,
        ItemStack stack,
        ItemDefinition definition);
}
