using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>InventoryGroup에 등록된 장비 컨테이너를 조작합니다.</summary>
    public sealed partial class EquipmentSystem : IEquipment
    {
        private readonly InventoryGroup group;
        private readonly string equipmentContainerId;
        private readonly IEquipmentEffectApplier effectApplier;

        public event Action<EquipmentChangeEventArgs> OnEquipmentChanged;

        public EquipmentSystem(
            InventoryGroup group,
            string equipmentContainerId,
            IEquipmentEffectApplier effectApplier = null)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));
            if (string.IsNullOrWhiteSpace(equipmentContainerId))
                throw new ArgumentException("Equipment container id is required.", nameof(equipmentContainerId));

            this.group = group;
            this.equipmentContainerId = equipmentContainerId;
            this.effectApplier = effectApplier ?? NullEquipmentEffectApplier.Instance;

            if (!group.TryGetContainer(equipmentContainerId, out _))
                throw new InvalidOperationException($"InventoryGroup does not contain equipment container '{equipmentContainerId}'.");
        }

        public EquipmentSystem(
            InventoryGroup group,
            EquipmentSetupSO setup,
            IEquipmentEffectApplier effectApplier = null)
            : this(group, GetContainerId(setup), effectApplier)
        {
        }

        public string EquipmentContainerId => equipmentContainerId;

        public int SlotCount =>
            TryGetEquipmentContainer(out InventoryContainer container) ? container.SlotCount : 0;

        public bool TryGetEquipmentContainer(out InventoryContainer container) =>
            group.TryGetContainer(equipmentContainerId, out container);

        public bool TryGetEquippedSlot(int equipSlotIndex, out InventorySlot slot)
        {
            slot = default;
            if (!TryGetEquipmentContainer(out InventoryContainer container))
                return false;

            return container.TryGetSlot(equipSlotIndex, out slot);
        }

        public bool IsEquipped(int equipSlotIndex) =>
            TryGetEquippedSlot(equipSlotIndex, out InventorySlot slot) && !slot.IsEmpty;

        private static string GetContainerId(EquipmentSetupSO setup) =>
            setup != null ? setup.ContainerId : throw new ArgumentNullException(nameof(setup));
    }
}