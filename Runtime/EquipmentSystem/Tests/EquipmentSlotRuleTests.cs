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
        public void Constructor_CopiesSlotCategories()
        {
            string[] categories =
            {
                EquipmentSlotCategories.Weapon,
                EquipmentSlotCategories.Head
            };
            var copiedRule = new EquipmentSlotRule(
                categories,
                EquipmentTestFixtures.CreateProfileSource(),
                (ItemType)EquipmentTestValues.EquipmentType);
            categories[0] = EquipmentSlotCategories.Head;
            EquipmentTestItemDatabase.Shared.TryGetDefinition(
                EquipmentTestValues.WeaponItemId,
                out ItemDefinition weapon);

            Assert.IsTrue(copiedRule.CanAccept(0, weapon));
        }

        [Test]
        public void SetupSnapshot_DoesNotModifySerializedCategories()
        {
            var setup = UnityEngine.ScriptableObject.CreateInstance<EquipmentSetupSO>();
            setup.SlotCount = 3;
            setup.SlotCategories = new[] { EquipmentSlotCategories.Weapon };
            string[] serializedCategories = setup.SlotCategories;

            string[] snapshot = setup.CreateSlotCategorySnapshot();

            Assert.AreSame(serializedCategories, setup.SlotCategories);
            Assert.AreEqual(1, setup.SlotCategories.Length);
            Assert.AreEqual(3, snapshot.Length);
            Assert.AreEqual(EquipmentSlotCategories.Weapon, snapshot[0]);
            Assert.AreEqual(EquipmentSlotCategories.Any, snapshot[1]);
            Assert.AreEqual(EquipmentSlotCategories.Any, snapshot[2]);

            UnityEngine.Object.DestroyImmediate(setup);
        }
        [Test]
        public void CanAccept_GeneralItem_ReturnsFalse()
        {
            EquipmentTestItemDatabase.Shared.TryGetDefinition(EquipmentTestValues.GeneralItemId, out ItemDefinition general);

            Assert.IsFalse(rule.CanAccept(0, general));
        }
    }
}
