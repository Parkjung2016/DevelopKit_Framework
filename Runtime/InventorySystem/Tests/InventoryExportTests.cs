using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryExportTests
    {
        [Test]
        public void ExportOccupiedSlots_ReturnsOnlyOccupiedEntries()
        {
            using InventoryContainer container = InventoryTestFixtures.CreateMainContainer();
            container.TryAddItemToSlot(1, InventoryTestItemDatabase.GeneralItemId, 2);
            container.TryAddItemToSlot(4, InventoryTestItemDatabase.EquipmentItemId, 1);

            InventorySlotSaveData[] slots = container.ExportOccupiedSlots();

            Assert.AreEqual(2, slots.Length);
            Assert.AreEqual(1, slots[0].SlotIndex);
            Assert.AreEqual(InventoryTestItemDatabase.GeneralItemId, slots[0].ItemId);
            Assert.AreEqual(2, slots[0].Count);
            Assert.AreEqual(4, slots[1].SlotIndex);
        }

        [Test]
        public void ExportOccupiedSlots_EmptyContainer_ReturnsEmptyArray()
        {
            using InventoryContainer container = InventoryTestFixtures.CreateMainContainer();

            InventorySlotSaveData[] slots = container.ExportOccupiedSlots();

            Assert.IsNotNull(slots);
            Assert.AreEqual(0, slots.Length);
        }

        [Test]
        public void GetOccupiedSlotIndices_FillsProvidedList()
        {
            using InventoryContainer container = InventoryTestFixtures.CreateMainContainer();
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);
            container.TryAddItemToSlot(2, InventoryTestItemDatabase.GeneralItemId, 1);

            var indices = new System.Collections.Generic.List<int>();
            container.GetOccupiedSlotIndices(indices);

            Assert.AreEqual(2, indices.Count);
            Assert.Contains(0, indices);
            Assert.Contains(2, indices);
        }
    }
}
