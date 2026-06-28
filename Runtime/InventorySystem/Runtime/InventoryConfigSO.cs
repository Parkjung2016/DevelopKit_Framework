using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public enum InventorySlotRuleMode
    {
        Any = 0,
        ItemType = 1
    }

    public enum InventoryCapacityRuleMode
    {
        None = 0,
        Weight = 1,
        SlotCount = 2
    }

    [CreateAssetMenu(fileName = "SO_InventoryConfig", menuName = "PJDev/SO/InventorySystem/Config")]
    public class InventoryConfigSO : ScriptableObject
    {
        [field: SerializeField] public string ContainerId { get; set; } = "main";
        [field: SerializeField] public ContainerKind Kind { get; set; } = (ContainerKind)InventoryEnumCore.MainContainerKindValue;
        [field: SerializeField] public int SlotCount { get; set; } = 20;

        [field: SerializeField] public InventorySlotRuleMode SlotRuleMode { get; set; } = InventorySlotRuleMode.Any;
        [field: SerializeField] public ItemType[] AllowedSlotTypes { get; set; } = Array.Empty<ItemType>();

        [field: SerializeField] public InventoryCapacityRuleMode CapacityRuleMode { get; set; } = InventoryCapacityRuleMode.None;
        [field: SerializeField] public float MaxWeight { get; set; } = 100f;
        [field: SerializeField] public int MaxOccupiedSlots { get; set; } = 20;

        public void NormalizeCapacityLimits()
        {
            if (SlotCount < 1)
                SlotCount = 1;

            if (MaxOccupiedSlots < 1)
                MaxOccupiedSlots = 1;

            if (MaxOccupiedSlots > SlotCount)
                MaxOccupiedSlots = SlotCount;
        }

        public InventoryContainerDescriptor CreateDescriptor()
        {
            NormalizeCapacityLimits();

            ISlotRule slotRule = SlotRuleMode == InventorySlotRuleMode.ItemType
                ? new ItemTypeSlotRule(AllowedSlotTypes ?? Array.Empty<ItemType>())
                : AnySlotRule.Instance;

            IContainerCapacityRule capacityRule = CapacityRuleMode switch
            {
                InventoryCapacityRuleMode.Weight => new WeightCapacityRule(MaxWeight),
                InventoryCapacityRuleMode.SlotCount => new SlotCountCapacityRule(MaxOccupiedSlots),
                _ => null
            };

            return new InventoryContainerDescriptor(ContainerId, Kind, slotRule, capacityRule);
        }

#if UNITY_EDITOR
        private void OnValidate() => NormalizeCapacityLimits();
#endif
    }
}
