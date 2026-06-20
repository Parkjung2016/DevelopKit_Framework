using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryChangeResultTests
    {
        private InventoryContainer container;

        [SetUp]
        public void SetUp() => container = InventoryTestFixtures.CreateMainContainer();

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
            container = null;
        }

        [Test]
        public void TryAddItem_FirstAcquisition_SetsItemWasAcquired()
        {
            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 1);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ItemWasAcquired);
            Assert.IsFalse(result.ItemWasDepleted);
            Assert.IsTrue(result.HasDefinition);
            Assert.AreEqual(InventoryTestItemDatabase.GeneralItemId, result.Definition.ItemId);
        }

        [Test]
        public void TryAddItem_PartialSuccess_SetsIsPartialSuccess()
        {
            InventoryTestFixtures.FillContainer(container, InventoryTestItemDatabase.GeneralItemId, 48);

            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 5);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.IsPartialSuccess);
            Assert.AreEqual(2, result.ProcessedCount);
            Assert.AreEqual(3, result.Remainder);
        }

        [Test]
        public void TryAddItem_RecordsSlotChanges()
        {
            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 6);

            Assert.AreEqual(2, result.SlotChanges.Length);
            Assert.AreEqual(5, result.SlotChanges[0].CurrentCount);
            Assert.AreEqual(1, result.SlotChanges[1].CurrentCount);
            Assert.AreEqual(6, result.TotalCountDelta);
            Assert.AreEqual("main", result.ContainerId);
            Assert.AreEqual(ContainerKind.Main, result.Kind);
        }

        [Test]
        public void ToDebugJson_ContainsExpectedFields()
        {
            InventoryChangeResult result = container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 2);
            string json = result.ToDebugJson();

            StringAssert.Contains("\"Success\": true", json);
            StringAssert.Contains("\"ItemId\": 1000", json);
            StringAssert.Contains("\"ProcessedCount\": 2", json);
            StringAssert.Contains("\"ContainerKind\": \"Main\"", json);
        }
    }
}
