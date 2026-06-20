using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class LootRoller
    {
        public static ItemStack[] Roll(in LootTableDefinition table, IItemDatabase database, Random random = null)
        {
            if (database == null || table.Entries == null || table.Entries.Length == 0)
                return Array.Empty<ItemStack>();

            random ??= new Random();
            return table.AllowDuplicateRolls
                ? RollWithReplacement(table, database, random)
                : RollWithoutReplacement(table, database, random);
        }

        public static ItemStack[] Roll(LootTableSO table, IItemDatabase database, Random random = null) =>
            table == null ? Array.Empty<ItemStack>() : Roll(table.ToDefinition(), database, random);

        public static InventoryChangeResult TryGrantLoot(
            InventoryGroup group,
            in LootTableDefinition table,
            Random random = null)
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

        public static InventoryChangeResult TryGrantLoot(
            InventoryGroup group,
            LootTableSO table,
            Random random = null) =>
            table == null
                ? InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.InvalidCount)
                : TryGrantLoot(group, table.ToDefinition(), random);

        private static ItemStack[] RollWithReplacement(
            in LootTableDefinition table,
            IItemDatabase database,
            Random random)
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
            Random random)
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
            Random random,
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

        private static bool TryPickEntry(LootEntry[] entries, Random random, out LootEntry picked)
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
            Random random,
            out int pickedIndex)
        {
            pickedIndex = -1;
            if (entries == null || candidateIndices == null || candidateIndices.Count == 0)
                return false;

            float totalWeight = 0f;
            for (int i = 0; i < candidateIndices.Count; i++)
            {
                LootEntry entry = entries[candidateIndices[i]];
                if (entry.Weight > 0f)
                    totalWeight += entry.Weight;
            }

            if (totalWeight <= 0f)
                return false;

            float roll = (float)(random.NextDouble() * totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < candidateIndices.Count; i++)
            {
                int entryIndex = candidateIndices[i];
                LootEntry entry = entries[entryIndex];
                if (entry.Weight <= 0f)
                    continue;

                cumulative += entry.Weight;
                if (roll <= cumulative)
                {
                    pickedIndex = entryIndex;
                    return true;
                }
            }

            for (int i = candidateIndices.Count - 1; i >= 0; i--)
            {
                int entryIndex = candidateIndices[i];
                if (entries[entryIndex].Weight > 0f)
                {
                    pickedIndex = entryIndex;
                    return true;
                }
            }

            return false;
        }

        private static int RollCount(LootEntry entry, ItemDefinition definition, Random random)
        {
            int min = entry.MinCount <= 0 ? 1 : entry.MinCount;
            int max = entry.MaxCount < min ? min : entry.MaxCount;

            if (!definition.IsStackable)
                return 1;

            return random.Next(min, max + 1);
        }
    }
}
