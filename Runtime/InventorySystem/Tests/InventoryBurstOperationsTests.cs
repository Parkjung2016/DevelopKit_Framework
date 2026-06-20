using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Burst;
using Unity.Collections;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryBurstOperationsTests
    {
        private NativeArray<SlotData> slots;
        private NativeList<int> changedSlots;

        [SetUp]
        public void SetUp()
        {
            slots = new NativeArray<SlotData>(5, Allocator.Persistent);
            changedSlots = new NativeList<int>(5, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (changedSlots.IsCreated)
                changedSlots.Dispose();

            if (slots.IsCreated)
                slots.Dispose();
        }

        [Test]
        public void TryAddItem_FillsExistingStacksThenEmptySlots()
        {
            slots[0] = new SlotData { ItemId = 1000, Count = 3 };

            InventoryBurstOperations.TryAddItem(
                ref slots,
                1000,
                4,
                maxStackSize: 5,
                isStackable: true,
                ref changedSlots,
                out int addedTotal,
                out int remainder,
                out int totalBefore);

            Assert.AreEqual(3, totalBefore);
            Assert.AreEqual(4, addedTotal);
            Assert.AreEqual(0, remainder);
            Assert.AreEqual(5, slots[0].Count);
            Assert.AreEqual(2, slots[1].Count);
        }

        [Test]
        public void HasItem_ReturnsEarlyWhenCountReached()
        {
            slots[0] = new SlotData { ItemId = 1000, Count = 2 };
            slots[1] = new SlotData { ItemId = 1000, Count = 5 };

            Assert.IsTrue(InventoryBurstOperations.HasItem(ref slots, 1000, 2));
            Assert.IsFalse(InventoryBurstOperations.HasItem(ref slots, 1000, 8));
        }

        [Test]
        public void TryRemoveItem_ReturnsTotalBeforeAndRemainder()
        {
            slots[0] = new SlotData { ItemId = 1000, Count = 3 };
            slots[2] = new SlotData { ItemId = 1000, Count = 2 };

            InventoryBurstOperations.TryRemoveItem(
                ref slots,
                1000,
                4,
                ref changedSlots,
                out int removedCount,
                out int remainder,
                out int totalBefore);

            Assert.AreEqual(5, totalBefore);
            Assert.AreEqual(4, removedCount);
            Assert.AreEqual(0, remainder);
            Assert.AreEqual(1, slots[2].Count);
        }

        [Test]
        public void TryMoveSlot_SwapsDifferentItemsWhenNotStackableMatch()
        {
            slots[0] = new SlotData { ItemId = 1000, Count = 1 };
            slots[1] = new SlotData { ItemId = 2000, Count = 1 };

            bool moved = InventoryBurstOperations.TryMoveSlot(
                ref slots,
                0,
                1,
                maxStackSize: 5,
                isStackable: true,
                ref changedSlots,
                out int processedCount,
                out int remainder);

            Assert.IsTrue(moved);
            Assert.AreEqual(2000, slots[0].ItemId);
            Assert.AreEqual(1000, slots[1].ItemId);
            Assert.AreEqual(0, processedCount);
            Assert.AreEqual(0, remainder);
        }
    }
}
