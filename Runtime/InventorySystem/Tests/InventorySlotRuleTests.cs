using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventorySlotRuleTests
    {
        [Test]
        public void ItemTypeSlotRule_AcceptsOnlyAllowedTypes()
        {
            var rule = new ItemTypeSlotRule((ItemType)InventoryTestValues.EquipmentType, (ItemType)InventoryTestValues.QuestType);
            InventoryTestItemDatabase.Shared.TryGetDefinition(InventoryTestItemDatabase.EquipmentItemId, out ItemDefinition equipment);
            InventoryTestItemDatabase.Shared.TryGetDefinition(InventoryTestItemDatabase.GeneralItemId, out ItemDefinition general);

            Assert.IsTrue(rule.CanAccept(0, equipment));
            Assert.IsFalse(rule.CanAccept(0, general));
        }

        [Test]
        public void AnySlotRule_AcceptsAllDefinitions()
        {
            InventoryTestItemDatabase.Shared.TryGetDefinition(InventoryTestItemDatabase.GeneralItemId, out ItemDefinition general);

            Assert.IsTrue(AnySlotRule.Instance.CanAccept(0, general));
        }

        [Test]
        public void EquipmentContainer_CanAcceptSlot_RejectsGeneralItem()
        {
            using InventoryContainer container = InventoryTestFixtures.CreateEquipmentContainer();
            InventoryTestItemDatabase.Shared.TryGetDefinition(InventoryTestItemDatabase.GeneralItemId, out ItemDefinition general);

            Assert.IsFalse(container.CanAcceptSlot(0, general));
        }

        [Test]
        public void TryAddItemToSlot_OnEquipmentContainer_DeniesGeneralItem()
        {
            using InventoryContainer container = InventoryTestFixtures.CreateEquipmentContainer();

            InventoryChangeResult result = container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.ItemTypeNotAllowed, result.Reason);
        }
    }
}
