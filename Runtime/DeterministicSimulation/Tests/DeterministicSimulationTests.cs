using NUnit.Framework;
using PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Tests
{
    public sealed class Fixed64Tests
    {
        [Test]
        public void Multiply_IsDeterministic()
        {
            Fixed64 a = Fixed64.FromInt(3) + Fixed64.Half;
            Fixed64 b = Fixed64.FromInt(2);
            Fixed64 result = a * b;
            Assert.AreEqual(7L, result.ToIntFloor());
        }

        [Test]
        public void Divide_IsDeterministic()
        {
            Fixed64 result = Fixed64.FromInt(10) / Fixed64.FromInt(4);
            Assert.AreEqual(2.5f, result.ToFloat(), 0.001f);
        }

        [Test]
        public void Sqrt_IsDeterministic()
        {
            Fixed64 result = FixedMath.Sqrt(Fixed64.FromInt(2));
            Assert.AreEqual(1.414f, result.ToFloat(), 0.01f);
        }
    }

    public sealed class DetRandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            DetRandom first = new(12345);
            DetRandom second = new(12345);

            for (int i = 0; i < 32; i++)
                Assert.AreEqual(first.NextUInt(), second.NextUInt());
        }

        [Test]
        public void DifferentSeed_ProducesDifferentSequence()
        {
            DetRandom first = new(1);
            DetRandom second = new(2);

            bool anyDifferent = false;
            for (int i = 0; i < 8; i++)
            {
                if (first.NextUInt() != second.NextUInt())
                    anyDifferent = true;
            }

            Assert.IsTrue(anyDifferent);
        }
    }

    public sealed class DeterministicSimulationTests
    {
        private struct MoveCommand
        {
            public int Delta;
        }

        private sealed class CounterSystem : ISimulationSystem
        {
            public int Value;

            public void OnSimulationReset(DeterministicSimulator simulation) => Value = 0;
            public void BeforeTick(DeterministicSimulator simulation) { }
            public void SimulateTick(DeterministicSimulator simulation) { }
        }

        private sealed class CommandSystem : ISimulationSystem
        {
            public int Value;

            public void OnSimulationReset(DeterministicSimulator simulation) => Value = 0;
            public void BeforeTick(DeterministicSimulator simulation) { }
            public void SimulateTick(DeterministicSimulator simulation) => Value += simulation.Random.NextInt(1, 4);
        }

        private sealed class CountingSystem : ISimulationSystem
        {
            public int BeforeCount;
            public int TickCount;

            public void OnSimulationReset(DeterministicSimulator simulation) { }
            public void BeforeTick(DeterministicSimulator simulation) => BeforeCount++;
            public void SimulateTick(DeterministicSimulator simulation) => TickCount++;
        }

        private sealed class SwapSystem : ISimulationSystem
        {
            private readonly ISimulationSystem replacement;
            private bool swapped;

            public SwapSystem(ISimulationSystem replacement) => this.replacement = replacement;

            public int TickCount { get; private set; }

            public void OnSimulationReset(DeterministicSimulator simulation) { }

            public void BeforeTick(DeterministicSimulator simulation)
            {
                if (swapped)
                    return;

                swapped = true;
                simulation.Unregister(this);
                simulation.Register(replacement);
            }

            public void SimulateTick(DeterministicSimulator simulation) => TickCount++;
        }

        [Test]
        public void SameSeedAndCommands_ProduceSameHash()
        {
            ulong hashA = RunSimulation(999, 120);
            ulong hashB = RunSimulation(999, 120);
            Assert.AreEqual(hashA, hashB);
        }

        [Test]
        public void CommandQueue_AppliesInTickOrder()
        {
            SimulationCommandQueue<MoveCommand> queue = new();
            CounterSystem counter = new();
            DeterministicSimulator simulation = new();
            simulation.Register(counter);
            simulation.Reset(new SimulationConfig(60, 1));

            queue.Enqueue(0, new MoveCommand { Delta = 2 });
            queue.Enqueue(1, new MoveCommand { Delta = 3 });

            simulation.Step(queue, command => counter.Value += command.Delta);
            Assert.AreEqual(2, counter.Value);

            simulation.Step(queue, command => counter.Value += command.Delta);
            Assert.AreEqual(5, counter.Value);
        }

        [Test]
        public void SystemChanges_AreAppliedAfterCurrentTick()
        {
            CountingSystem replacement = new();
            SwapSystem current = new(replacement);
            DeterministicSimulator simulation = new();
            simulation.Register(current);
            simulation.Reset();

            simulation.Step();

            Assert.AreEqual(1, current.TickCount);
            Assert.AreEqual(0, replacement.TickCount);
            Assert.AreEqual(1, simulation.SystemCount);

            simulation.Step();

            Assert.AreEqual(1, replacement.BeforeCount);
            Assert.AreEqual(1, replacement.TickCount);
        }

        [Test]
        public void ClearFromTick_RemovesOnlyCurrentAndFutureCommands()
        {
            SimulationCommandQueue<MoveCommand> queue = new();
            queue.Enqueue(1, new MoveCommand { Delta = 1 });
            queue.Enqueue(2, new MoveCommand { Delta = 2 });
            queue.Enqueue(3, new MoveCommand { Delta = 3 });

            queue.ClearFromTick(2);

            Assert.AreEqual(1, queue.TickCount);
            Assert.AreEqual(1, queue.GetCommandCount(1));
            Assert.AreEqual(0, queue.GetCommandCount(2));
        }

        private static ulong RunSimulation(ulong seed, int ticks)
        {
            CommandSystem system = new();
            DeterministicSimulator simulation = new();
            simulation.Register(system);
            simulation.Reset(new SimulationConfig(60, seed));
            simulation.Step(ticks);
            return simulation.ComputeStateHash((ulong)system.Value);
        }
    }
}
