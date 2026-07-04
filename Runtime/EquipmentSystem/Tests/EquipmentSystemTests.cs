using NUnit.Framework;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using EquipmentService = PJDev.DevelopKit.Framework.EquipmentSystem.Runtime.EquipmentSystem;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Tests
{
    [TestFixture]
    public sealed class EquipmentSystemTests
    {
        private InventoryGroup group;
        private InventoryContainer main;
        private InventoryContainer equipment;
        private EquipmentService equipmentSystem;
        private RecordingEffectApplier effectApplier;

        [SetUp]
        public void SetUp()
        {
            group = EquipmentTestFixtures.CreateGroup();
            group.TryGetContainer("main", out main);
            group.TryGetContainer("equipment", out equipment);

            var setup = UnityEngine.ScriptableObject.CreateInstance<EquipmentSetupSO>();
            setup.ContainerId = "equipment";
            setup.SlotCount = 6;

            effectApplier = new RecordingEffectApplier();
            equipmentSystem = new EquipmentService(group, setup, effectApplier);
        }

        [TearDown]
        public void TearDown()
        {
            group?.Dispose();
            group = null;
            main = null;
            equipment = null;
            equipmentSystem = null;
            effectApplier = null;
        }

        [Test]
        public void TryEquipFromContainer_PlacesItemInMatchingSlot()
        {
            main.TryAddItemToSlot(0, EquipmentTestValues.WeaponItemId, 1);

            InventoryChangeResult result = equipmentSystem.TryEquipFromContainer("main", 0, 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(EquipmentTestValues.WeaponItemId, equipment.GetSlot(0).Stack.ItemId);
            Assert.IsTrue(main.GetSlot(0).IsEmpty);
            Assert.AreEqual(1, effectApplier.EquipCount);
        }

        [Test]
        public void TryEquipFromContainer_WrongSlotCategory_ReturnsSlotRuleDenied()
        {
            main.TryAddItemToSlot(0, EquipmentTestValues.HeadItemId, 1);

            InventoryChangeResult result = equipmentSystem.TryEquipFromContainer("main", 0, 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.SlotRuleDenied, result.Reason);
        }

        [Test]
        public void TryEquipFromContainer_OccupiedSlot_SwapsWithInventoryItem()
        {
            main.TryAddItemToSlot(0, EquipmentTestValues.WeaponItemId, 1);
            equipment.TryAddItemToSlot(0, EquipmentTestValues.WeaponItemId, 1);
            long previousInstance = equipment.GetSlot(0).Stack.InstanceId;

            main.TryAddItemToSlot(1, EquipmentTestValues.WeaponItemId, 1);

            InventoryChangeResult result = equipmentSystem.TryEquipFromContainer("main", 1, 0);

            Assert.IsTrue(result.Success);
            Assert.AreNotEqual(previousInstance, equipment.GetSlot(0).Stack.InstanceId);
            Assert.AreEqual(previousInstance, main.GetSlot(1).Stack.InstanceId);
        }

        [Test]
        public void TryUnequipToFirstAvailable_MovesItemToMain()
        {
            equipment.TryAddItemToSlot(0, EquipmentTestValues.WeaponItemId, 1);

            InventoryChangeResult result = equipmentSystem.TryUnequipToFirstAvailable(0, "main");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(equipment.GetSlot(0).IsEmpty);
            Assert.AreEqual(EquipmentTestValues.WeaponItemId, main.GetSlot(0).Stack.ItemId);
            Assert.AreEqual(1, effectApplier.UnequipCount);
        }

        [Test]
        public void TrySwapEquippedSlots_InvalidCategorySwap_ReturnsSlotRuleDenied()
        {
            equipment.TryAddItemToSlot(0, EquipmentTestValues.WeaponItemId, 1);
            equipment.TryAddItemToSlot(1, EquipmentTestValues.HeadItemId, 1);

            InventoryChangeResult result = equipmentSystem.TrySwapEquippedSlots(0, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.SlotRuleDenied, result.Reason);
        }

        [Test]
        public void TrySwapEquippedSlots_AllowsSwap_WhenTargetSlotAcceptsItem()
        {
            equipment.TryAddItemToSlot(0, EquipmentTestValues.WeaponItemId, 1);
            equipment.TryAddItemToSlot(5, EquipmentTestValues.WeaponItemId, 1);

            InventoryChangeResult result = equipmentSystem.TrySwapEquippedSlots(0, 5);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(EquipmentTestValues.WeaponItemId, equipment.GetSlot(0).Stack.ItemId);
            Assert.AreEqual(EquipmentTestValues.WeaponItemId, equipment.GetSlot(5).Stack.ItemId);
            Assert.AreNotEqual(equipment.GetSlot(0).Stack.InstanceId, equipment.GetSlot(5).Stack.InstanceId);
        }

        [Test]
        public void CompositeEffectApplier_ForwardsToAllAppliers()
        {
            var applierA = new RecordingEffectApplier();
            var applierB = new RecordingEffectApplier();
            var composite = new CompositeEquipmentEffectApplier(applierA, applierB);

            group.ItemDatabase.TryGetDefinition(EquipmentTestValues.WeaponItemId, out ItemDefinition definition);
            var stack = new ItemStack(EquipmentTestValues.WeaponItemId, 1);

            composite.OnEquipped(0, stack, definition);
            composite.OnUnequipped(0, stack, definition);

            Assert.AreEqual(1, applierA.EquipCount);
            Assert.AreEqual(1, applierB.EquipCount);
            Assert.AreEqual(1, applierA.UnequipCount);
            Assert.AreEqual(1, applierB.UnequipCount);
        }

        private sealed class RecordingEffectApplier : IEquipmentEffectApplier
        {
            public int EquipCount { get; private set; }
            public int UnequipCount { get; private set; }

            public void OnEquipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition) => EquipCount++;

            public void OnUnequipped(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition) => UnequipCount++;
        }
    }
}
