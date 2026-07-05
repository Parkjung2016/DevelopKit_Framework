namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary><see cref="IEquipmentVisualSpawner"/>에 전달하는 스폰 요청입니다.</summary>
    public readonly struct EquipmentVisualSpawnRequest
    {
        public EquipmentVisualSpawnRequest(int itemId, string assetKey, int equipSlotIndex, long instanceId = 0)
        {
            ItemId = itemId;
            AssetKey = assetKey;
            EquipSlotIndex = equipSlotIndex;
            InstanceId = instanceId;
        }

        public int ItemId { get; }
        public string AssetKey { get; }
        public int EquipSlotIndex { get; }
        public long InstanceId { get; }
    }
}
