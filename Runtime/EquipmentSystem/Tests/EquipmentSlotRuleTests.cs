using NUnit.Framework;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Tests
{
    [TestFixture]
    public sealed class EquipmentSlotRuleTests
    {
        private EquipmentSlotRule rule;

        [SetUp]
        public void SetUp()
        {
            rule = new EquipmentSlotRule(
                new[] { EquipmentSlotCategories.Weapon, EquipmentSlotCategories.Head },
                EquipmentTestFixtures.CreateProfileSource(),
                (ItemType)EquipmentTestValues.EquipmentType);
        }

        [Test]
        public void CanAccept_MatchingCategory_ReturnsTrue()
        {
            EquipmentTestItemDatabase.Shared.TryGetDefinition(EquipmentTestValues.WeaponItemId, out ItemDefinition weapon);

            Assert.IsTrue(rule.CanAccept(0, weapon));
        }

        [Test]
        public void CanAccept_WrongSlotCategory_ReturnsFalse()
        {
            EquipmentTestItemDatabase.Shared.TryGetDefinition(EquipmentTestValues.HeadItemId, out ItemDefinition head);

            Assert.IsFalse(rule.CanAccept(0, head));
        }

        [Test]
        public void CanAccept_GeneralItem_ReturnsFalse()
        {
            EquipmentTestItemDatabase.Shared.TryGetDefinition(EquipmentTestValues.GeneralItemId, out ItemDefinition general);

            Assert.IsFalse(rule.CanAccept(0, general));
        }
    }
}
