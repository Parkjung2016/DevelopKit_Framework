using NUnit.Framework;
using PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime;
using PJDev.DevelopKit.Framework.RandomSystem.Runtime;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Tests
{
    public sealed class RandomSourceTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            IRandomSource first = RandomProvider.Create(42);
            IRandomSource second = RandomProvider.Create(42);

            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(first.NextInt(0, 100), second.NextInt(0, 100));
                Assert.AreEqual(first.NextDouble(), second.NextDouble());
            }
        }

        [Test]
        public void SimulationRandomSource_SyncsBackToSimulation()
        {
            var baseline = new DetRandomSource(99);
            int expectedFirst = baseline.NextInt(0, 1000);
            int expectedSecond = baseline.NextInt(0, 1000);

            var simulation = new DeterministicSimulator();
            simulation.Reset(new SimulationConfig(60, 99));

            DetRandomSource source = simulation.CreateRandomSource();
            Assert.AreEqual(expectedFirst, source.NextInt(0, 1000));
            simulation.SyncRandom(source);

            DetRandomSource replay = simulation.CreateRandomSource();
            Assert.AreEqual(expectedSecond, replay.NextInt(0, 1000));
        }
    }
}