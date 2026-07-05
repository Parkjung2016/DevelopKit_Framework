using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class LootRoller
    {
        public static ItemStack[] Roll(
            in LootTableDefinition table,
            IItemDatabase database = null,
            IRandomSource random = null)
        {
            database = ItemCatalog.Resolve(database);
            if (table.Entries == null || table.Entries.Length == 0)
                return Array.Empty<ItemStack>();

            random ??= RandomSources.System();
            return table.AllowDuplicateRolls
                ? RollWithReplacement(table, database, random)
                : RollWithoutReplacement(table, database, random);
        }

        public static InventoryChangeResult TryGrantLoot(
            InventoryGroup group,
            in LootTableDefinition table,
            IRandomSource random = null)
        {
            if (group == null)
                return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.DatabaseNotReady);

            ItemStack[] loot = Roll(table, group.ItemDatabase, random);
            InventoryChangeResult lastResult = InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.NoChange);

            using InventoryGroupTransaction transaction = InventoryGroupTransaction.Begin(group);

            for (int i = 0; i < loot.Length; i++)
            {
                ItemStack stack = loot[i];
                lastResult = group.TryAddItem(stack.ItemId, stack.Count);
                if (!lastResult.Success || lastResult.Remainder > 0)
                    return InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.NoSpace, stack.ItemId, stack.Count);
            }

            transaction.Commit();
            return lastResult;
        }

        private static ItemStack[] RollWithReplacement(
            in LootTableDefinition table,
            IItemDatabase database,
            IRandomSource random)
        {
            var results = new List<ItemStack>(table.RollCount);

            for (int rollIndex = 0; rollIndex < table.RollCount; rollIndex++)
            {
                if (!TryPickEntry(table.Entries, random, out LootEntry entry))
                    continue;

                if (!TryCreateStack(entry, database, random, out ItemStack stack))
                    continue;

                results.Add(stack);
            }

            return results.ToArray();
        }

        private static ItemStack[] RollWithoutReplacement(
            in LootTableDefinition table,
            IItemDatabase database,
            IRandomSource random)
        {
            LootEntry[] entries = table.Entries;
            var candidates = new List<int>(entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Weight > 0f)
                    candidates.Add(i);
            }

            int rollLimit = Math.Min(table.RollCount, candidates.Count);
            var results = new List<ItemStack>(rollLimit);

            for (int rollIndex = 0; rollIndex < rollLimit; rollIndex++)
            {
                if (!TryPickEntryIndex(entries, candidates, random, out int pickedIndex))
                    break;

                candidates.Remove(pickedIndex);

                if (!TryCreateStack(entries[pickedIndex], database, random, out ItemStack stack))
                    continue;

                results.Add(stack);
            }

            return results.ToArray();
        }

        private static bool TryCreateStack(
            LootEntry entry,
            IItemDatabase database,
            IRandomSource random,
            out ItemStack stack)
        {
            stack = default;
            if (!database.TryGetDefinition(entry.ItemId, out ItemDefinition definition))
                return false;

            int count = RollCount(entry, definition, random);
            if (count <= 0)
                return false;

            stack = new ItemStack(entry.ItemId, count);
            return true;
        }

        private static bool TryPickEntry(LootEntry[] entries, IRandomSource random, out LootEntry picked)
        {
            picked = default;
            if (entries == null || entries.Length == 0)
                return false;

            var indices = new List<int>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Weight > 0f)
                    indices.Add(i);
            }

            if (!TryPickEntryIndex(entries, indices, random, out int pickedIndex))
                return false;

            picked = entries[pickedIndex];
            return true;
        }

        private static bool TryPickEntryIndex(
            LootEntry[] entries,
            IReadOnlyList<int> candidateIndices,
            IRandomSource random,
            out int pickedIndex) =>
            RandomUtility.TryPickWeightedIndex(
                candidateIndices,
                index => entries[index].Weight,
                random,
                out pickedIndex);

        private static int RollCount(LootEntry entry, ItemDefinition definition, IRandomSource random)
        {
            int min = entry.MinCount <= 0 ? 1 : entry.MinCount;
            int max = entry.MaxCount < min ? min : entry.MaxCount;

            if (!definition.IsStackable)
                return 1;

            return random.NextInt(min, max + 1);
        }
    }
}
