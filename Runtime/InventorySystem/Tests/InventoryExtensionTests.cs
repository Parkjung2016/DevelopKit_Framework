using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventoryExtensionTests
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
        public void CanAddItem_WhenSpaceAvailable_ReturnsTrueWithFullAddableCount()
        {
            bool canAdd = container.CanAddItem(
                InventoryTestItemDatabase.GeneralItemId,
                3,
                out InventoryFailReason reason,
                out int addableCount);

            Assert.IsTrue(canAdd);
            Assert.AreEqual(InventoryFailReason.None, reason);
            Assert.AreEqual(3, addableCount);
        }

        [Test]
        public void CanAddItem_WhenInventoryFull_ReturnsFalse()
        {
            InventoryTestFixtures.FillContainer(container, InventoryTestItemDatabase.GeneralItemId, 50);

            bool canAdd = container.CanAddItem(
                InventoryTestItemDatabase.GeneralItemId,
                1,
                out InventoryFailReason reason,
                out int addableCount);

            Assert.IsFalse(canAdd);
            Assert.AreEqual(InventoryFailReason.NoSpace, reason);
            Assert.AreEqual(0, addableCount);
        }

        [Test]
        public void CanRemoveItem_WhenInsufficientItems_ReturnsFalse()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 2);

            bool canRemove = container.CanRemoveItem(
                InventoryTestItemDatabase.GeneralItemId,
                5,
                out InventoryFailReason reason);

            Assert.IsFalse(canRemove);
            Assert.AreEqual(InventoryFailReason.InsufficientItemCount, reason);
        }

        [Test]
        public void TrySplitStack_MovesPartialStackToEmptySlot()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 4);

            InventoryChangeResult result = container.TrySplitStack(0, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(InventoryChangeType.Split, result.ChangeType);
            Assert.AreEqual(2, container.GetSlot(0).Stack.Count);
            Assert.AreEqual(2, container.GetSlot(1).Stack.Count);
        }

        [Test]
        public void TryDropItemFromSlot_QuestItem_ReturnsItemActionDenied()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.QuestItemId, 1);

            InventoryChangeResult result = container.TryDropItemFromSlot(0, 1);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.ItemActionDenied, result.Reason);
            Assert.AreEqual(1, container.GetItemCount(InventoryTestItemDatabase.QuestItemId));
        }

        [Test]
        public void TryDropItemFromSlot_AllowedItem_RemovesFromInventory()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 3);

            InventoryChangeResult result = container.TryDropItemFromSlot(0, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(InventoryChangeType.Drop, result.ChangeType);
            Assert.AreEqual(1, container.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
            Assert.Greater(container.Revision, 0);
        }

        [Test]
        public void TryTradeItemFromSlot_RemovesRequestedCount()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 4);

            InventoryChangeResult result = container.TryTradeItemFromSlot(0, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(InventoryChangeType.Trade, result.ChangeType);
            Assert.AreEqual(2, container.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void TryUseItem_WhenHandlerAllows_DelegatesToHandler()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);
            var handler = new TestUseHandler();

            InventoryChangeResult result = container.TryUseItem(0, handler);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(InventoryChangeType.Use, result.ChangeType);
            Assert.AreEqual(1, handler.UseCallCount);
        }

        [Test]
        public void TryUseItem_WhenHandlerDenies_ReturnsItemActionDenied()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 1);
            var handler = new TestUseHandler { CanUseResult = false };

            InventoryChangeResult result = container.TryUseItem(0, handler);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(InventoryFailReason.ItemActionDenied, result.Reason);
            Assert.AreEqual(0, handler.UseCallCount);
        }

        [Test]
        public void FindSlotsWithItem_ReturnsAllMatchingSlots()
        {
            container.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 8);

            var results = new List<int>();
            container.FindSlotsWithItem(InventoryTestItemDatabase.GeneralItemId, results);

            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void TryFindStackableSlot_FindsPartialStack()
        {
            container.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 2);

            bool found = container.TryFindStackableSlot(InventoryTestItemDatabase.GeneralItemId, out int slotIndex);

            Assert.IsTrue(found);
            Assert.AreEqual(0, slotIndex);
        }

        [Test]
        public void EquipmentItem_ReceivesUniqueInstanceId()
        {
            container.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);

            long instanceId = container.GetSlot(0).InstanceId;

            Assert.Greater(instanceId, 0);
        }

        [Test]
        public void Export_And_Import_PreservesInstanceId()
        {
            container.TryAddItem(InventoryTestItemDatabase.EquipmentItemId, 1);
            long instanceId = container.GetSlot(0).InstanceId;

            InventoryContainerSaveData saveData = InventorySerializer.Export(container);
            container.ClearAll();
            InventorySerializer.Import(container, saveData);

            Assert.AreEqual(instanceId, container.GetSlot(0).InstanceId);
        }

        private sealed class TestUseHandler : IItemUseHandler
        {
            public bool CanUseResult = true;
            public int UseCallCount;

            public bool CanUse(IInventoryContainer container, int slotIndex, in ItemDefinition definition) =>
                CanUseResult;

            public InventoryChangeResult TryUse(IInventoryContainer container, int slotIndex)
            {
                UseCallCount++;
                return InventoryChangeResult.Succeed(
                    InventoryChangeType.Use,
                    container.TryGetSlot(slotIndex, out InventorySlot slot) ? slot.Stack.ItemId : 0,
                    default,
                    default,
                    1,
                    1,
                    0,
                    1,
                    0,
                    slotIndex,
                    -1,
                    false,
                    false,
                    container.ContainerId,
                    ContainerKind.Main,
                    null,
                    new[] { slotIndex },
                    System.Array.Empty<InventorySlotChange>());
            }
        }
    }

    [TestFixture]
    public sealed class InventoryCraftTests
    {
        private InventoryContainer main;
        private InventoryGroup group;

        [SetUp]
        public void SetUp()
        {
            main = InventoryTestFixtures.CreateMainContainer();
            group = InventoryTestFixtures.CreateGroup(main);
        }

        [TearDown]
        public void TearDown()
        {
            group?.Dispose();
            group = null;
            main = null;
        }

        [Test]
        public void CanCraft_WhenCostsMissing_ReturnsFalse()
        {
            bool canCraft = group.CanCraft(
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 5) },
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 1) },
                out InventoryFailReason reason);

            Assert.IsFalse(canCraft);
            Assert.AreEqual(InventoryFailReason.NoChange, reason);
        }

        [Test]
        public void TryCraft_ConsumesCostsAndAddsRewards()
        {
            main.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 5);

            InventoryChangeResult result = group.TryCraft(
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 3) },
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 2) });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(4, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void TryCraft_WhenInventoryFull_ReturnsFailWithoutMutating()
        {
            InventoryTestFixtures.FillContainer(main, InventoryTestItemDatabase.GeneralItemId, 50);

            InventoryChangeResult result = group.TryCraft(
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 2) },
                new[] { new InventoryRecipeEntry(InventoryTestItemDatabase.GeneralItemId, 5) });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(50, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }
    }

    [TestFixture]
    public sealed class InventoryGroupTransactionTests
    {
        private InventoryContainer main;
        private InventoryGroup group;

        [SetUp]
        public void SetUp()
        {
            main = InventoryTestFixtures.CreateMainContainer();
            group = InventoryTestFixtures.CreateGroup(main);
        }

        [TearDown]
        public void TearDown()
        {
            group?.Dispose();
            group = null;
            main = null;
        }

        [Test]
        public void BeginTransaction_WithoutCommit_RollsBackOnDispose()
        {
            main.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 5);

            using (group.BeginTransaction())
                main.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 3);

            Assert.AreEqual(5, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }

        [Test]
        public void BeginTransaction_WithCommit_PersistsChanges()
        {
            main.TryAddItem(InventoryTestItemDatabase.GeneralItemId, 5);

            using (InventoryGroupTransaction transaction = group.BeginTransaction())
            {
                main.TryRemoveItem(InventoryTestItemDatabase.GeneralItemId, 2);
                transaction.Commit();
            }

            Assert.AreEqual(3, group.GetItemCount(InventoryTestItemDatabase.GeneralItemId));
        }
    }

    [TestFixture]
    public sealed class InventoryDeltaTests
    {
        private InventoryContainer main;
        private InventoryGroup group;

        [SetUp]
        public void SetUp()
        {
            main = InventoryTestFixtures.CreateMainContainer();
            group = InventoryTestFixtures.CreateGroup(main);
        }

        [TearDown]
        public void TearDown()
        {
            group?.Dispose();
            group = null;
            main = null;
        }

        [Test]
        public void ComputeDelta_ReturnsChangedSlotsSinceBaseline()
        {
            InventoryGroupSaveData baseline = group.ExportSaveData();
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 3);

            InventoryGroupDelta delta = group.ComputeDelta(baseline);

            Assert.AreEqual(1, delta.Containers.Length);
            Assert.AreEqual(1, delta.Containers[0].Slots.Length);
            Assert.AreEqual(0, delta.Containers[0].Slots[0].SlotIndex);
            Assert.AreEqual(3, delta.Containers[0].Slots[0].CurrentCount);
        }

        [Test]
        public void ApplyContainerDelta_RestoresBaselineState()
        {
            InventoryGroupSaveData baseline = group.ExportSaveData();
            main.TryAddItemToSlot(0, InventoryTestItemDatabase.GeneralItemId, 3);
            InventoryGroupDelta delta = group.ComputeDelta(baseline);

            main.ClearAll();
            InventoryDeltaComputer.ApplyContainerDelta(main, delta.Containers[0]);

            Assert.AreEqual(3, main.GetSlot(0).Stack.Count);
        }
    }
}
