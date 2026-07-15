using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime.Tests
{
    [TestFixture]
    public sealed class RandomSystemTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var first = new RandomGenerator(1234);
            var second = new RandomGenerator(1234);

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(first.NextUInt(), second.NextUInt());
        }

        [Test]
        public void Snapshot_RestoresSequence()
        {
            var random = new RandomGenerator(55);
            random.NextUInt();
            RandomSnapshot snapshot = random.Snapshot;
            uint expected = random.NextUInt();

            random.Restore(snapshot);

            Assert.AreEqual(expected, random.NextUInt());
        }

        [Test]
        public void NextInt_StaysInsideLargeSignedRange()
        {
            var random = new RandomGenerator(7);
            for (int i = 0; i < 10000; i++)
            {
                int value = random.NextInt(int.MinValue, int.MaxValue);
                Assert.GreaterOrEqual(value, int.MinValue);
                Assert.Less(value, int.MaxValue);
            }
        }

        [Test]
        public void NextFloatAndDouble_AreHalfOpen()
        {
            var random = new RandomGenerator(19);
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(random.NextFloat(), Is.InRange(0f, 0.999999999f));
                Assert.That(random.NextDouble(), Is.InRange(0d, 0.9999999999999999d));
            }
        }

        [Test]
        public void ShuffleBag_ContainsExactEntriesEveryCycle()
        {
            var bag = new ShuffleBag<string>(new RandomGenerator(3));
            bag.Add("Common", 3);
            bag.Add("Rare", 1);

            for (int cycle = 0; cycle < 10; cycle++)
            {
                int common = 0;
                int rare = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (bag.Next() == "Common")
                        common++;
                    else
                        rare++;
                }

                Assert.AreEqual(3, common);
                Assert.AreEqual(1, rare);
            }
        }

        [Test]
        public void BalancedChance_KeepsCountWithinOneResult()
        {
            const double probability = 0.23d;
            const int rolls = 1000;
            var chance = new BalancedChance(new RandomGenerator(11));
            int success = 0;

            for (int i = 0; i < rolls; i++)
            {
                if (chance.Roll(probability))
                    success++;
            }

            Assert.Less(Math.Abs(success - rolls * probability), 1.000001d);
        }

        [Test]
        public void WeightedTable_SkipsInvalidWeights()
        {
            var entries = new[]
            {
                new WeightedValue("Never", 0d),
                new WeightedValue("Always", 1d),
                new WeightedValue("Invalid", double.NaN)
            };
            var table = new WeightedTable<WeightedValue>(entries, static item => item.Weight);

            for (int i = 0; i < 20; i++)
                Assert.AreEqual("Always", table.Pick(new RandomGenerator((ulong)i + 1)).Name);
        }

        [Test]
        public void Shuffle_ProducesPermutationWithoutLoss()
        {
            var values = new List<int> { 1, 2, 3, 4, 5 };
            RandomPick.Shuffle(values, new RandomGenerator(9));
            values.Sort();

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, values);
        }

        private readonly struct WeightedValue
        {
            public WeightedValue(string name, double weight)
            {
                Name = name;
                Weight = weight;
            }

            public string Name { get; }
            public double Weight { get; }
        }
    }
}
