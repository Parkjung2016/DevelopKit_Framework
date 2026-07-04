using PJDev.DevelopKit.Framework.SocketSystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary><see cref="IEquipmentVisualSpawner"/>에 전달하는 스폰 요청입니다.</summary>
    public readonly struct EquipmentVisualSpawnRequest
    {
        public EquipmentVisualSpawnRequest(
            int equipSlotIndex,
            string slotCategory,
            ObjectSocket socket,
            in EquipmentVisualDefinition definition)
        {
            EquipSlotIndex = equipSlotIndex;
            SlotCategory = slotCategory;
            Socket = socket;
            Definition = definition;
        }

        public int EquipSlotIndex { get; }
        public string SlotCategory { get; }
        public ObjectSocket Socket { get; }
        public EquipmentVisualDefinition Definition { get; }
    }
}
