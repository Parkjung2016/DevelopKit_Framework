using System;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>장비 슬롯 인덱스 → ObjectSocket 이름. Left/Right 등 착용 위치는 슬롯으로 결정합니다.</summary>
    [Serializable]
    public struct EquipmentVisualSlotSocketBinding
    {
        public int EquipSlotIndex;
        public string SocketKey;
    }
}
