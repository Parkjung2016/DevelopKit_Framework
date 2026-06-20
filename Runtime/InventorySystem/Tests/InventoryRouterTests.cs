using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryRouterTests
    {
        private InventoryContainer main;
        private InventoryContainer equipment;
        private InventoryContainer quest;
        private InventoryGroup group;
        private DefaultItemContainerRouter router;

        [SetUp]
        public void SetUp()
        {
            main = InventoryTestFixtures.CreateMainContainer();
            equipment = InventoryTestFixtures.CreateEquipmentContainer();
            quest = InventoryTestFixtures.CreateQuestContainer();
            group = InventoryTestFixtures.CreateGroup(main, equipment, quest);
            router = DefaultItemContainerRouter.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            group?.Dispose();
            group = null;
            main = null;
            equipment = null;
            quest = null;
        }

        [Test]
        public void TryResolveContainer_QuestItem_UsesQuestContainer()
        {
            Assert.IsTrue(InventoryTestItemDatabase.Shared.TryGetDefinition(
                InventoryTestItemDatabase.QuestItemId,
                out ItemDefinition definition));

            Assert.IsTrue(router.TryResolveContainer(group, definition, out IInventoryContainer resolved));
            Assert.AreEqual(ContainerKind.Quest, resolved.Kind);
            Assert.AreEqual("quest", resolved.ContainerId);
        }

        [Test]
        public void TryResolveContainer_EquipmentItem_UsesEquipmentContainer()
        {
            InventoryTestItemDatabase.Shared.TryGetDefinition(InventoryTestItemDatabase.EquipmentItemId, out ItemDefinition definition);

            Assert.IsTrue(router.TryResolveContainer(group, definition, out IInventoryContainer resolved));
            Assert.AreEqual(ContainerKind.Equipment, resolved.Kind);
        }

        [Test]
        public void TryResolveContainer_UnknownItemType_FallsBackToMain()
        {
            var customDefinition = new ItemDefinition(9001, itemType: (ItemType)999);
            InventoryTestItemDatabase.Shared.Register(customDefinition);

            Assert.IsTrue(router.TryResolveContainer(group, customDefinition, out IInventoryContainer resolved));
            Assert.AreEqual(ContainerKind.Main, resolved.Kind);
        }
    }
}
