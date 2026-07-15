using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.RandomSystem.Runtime;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>고정 틱 기반 결정론 시뮬레이션 루프.</summary>
    public sealed class DeterministicSimulator
    {
        private SimulationConfig config = SimulationConfig.Default;
        private readonly List<ISimulationSystem> systems = new();

        public SimulationConfig Config => config;

        public int Tick { get; private set; }

        public DetRandom Random { get; private set; }

        public bool IsRunning { get; private set; }

        public void Register(ISimulationSystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (!systems.Contains(system))
                systems.Add(system);
        }

        public void Unregister(ISimulationSystem system) => systems.Remove(system);

        public void Configure(SimulationConfig configValue) => config = configValue;

        public void Reset(SimulationConfig config, int startTick = 0)
        {
            Configure(config);
            Reset(startTick);
        }

        public void Reset(int startTick = 0)
        {
            Tick = startTick;
            Random = new DetRandom(config.Seed);
            IsRunning = true;

            for (int i = 0; i < systems.Count; i++)
                systems[i].OnSimulationReset(this);
        }

        public void Stop() => IsRunning = false;

        public void Step()
        {
            if (!IsRunning)
                return;

            for (int i = 0; i < systems.Count; i++)
                systems[i].BeforeTick(this);

            for (int i = 0; i < systems.Count; i++)
                systems[i].SimulateTick(this);

            Tick++;
        }

        public void Step(int count)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
                Step();
        }

        public void Step<TCommand>(
            SimulationCommandQueue<TCommand> commandQueue,
            Action<TCommand> applyCommand)
            where TCommand : struct
        {
            if (!IsRunning)
                return;

            if (commandQueue == null)
                throw new ArgumentNullException(nameof(commandQueue));
            if (applyCommand == null)
                throw new ArgumentNullException(nameof(applyCommand));

            for (int i = 0; i < systems.Count; i++)
                systems[i].BeforeTick(this);

            ReadOnlySpan<TCommand> commands = commandQueue.GetCommands(Tick);
            for (int i = 0; i < commands.Length; i++)
                applyCommand(commands[i]);

            for (int i = 0; i < systems.Count; i++)
                systems[i].SimulateTick(this);

            Tick++;
        }

        public ulong ComputeStateHash(ulong customState = 0) =>
            DeterministicHasher.HashSimulationState(Tick, Random.State, customState);

        /// <summary>시뮬 RNG 상태를 공유하는 <see cref="IRandomSource"/>를 만듭니다. 사용 후 <see cref="SyncRandom"/>을 호출하세요.</summary>
        public DetRandomSource CreateRandomSource() => new DetRandomSource(Random);

        public void SyncRandom(DetRandomSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            Random = source.Random;
        }
    }
}
