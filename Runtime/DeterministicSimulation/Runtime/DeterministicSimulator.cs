using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.RandomSystem.Runtime;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>등록된 시스템을 고정된 순서로 실행하는 결정론 시뮬레이터입니다.</summary>
    public sealed class DeterministicSimulator
    {
        private readonly List<ISimulationSystem> systems = new();
        private readonly List<ISimulationSystem> pendingAdds = new();
        private readonly List<ISimulationSystem> pendingRemoves = new();
        private SimulationConfig config = SimulationConfig.Default;
        private bool isInvokingSystems;

        /// <summary>현재 시뮬레이션에 적용된 설정입니다.</summary>
        public SimulationConfig Config => config;
        /// <summary>다음에 실행할 틱 번호입니다.</summary>
        public int Tick { get; private set; }
        /// <summary>현재 결정론 난수 상태입니다.</summary>
        public DetRandom Random { get; private set; }
        /// <summary><see cref="Step()"/> 호출로 틱을 진행할 수 있는 상태인지 나타냅니다.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>현재 등록되어 실행되는 시스템 개수입니다.</summary>
        public int SystemCount => systems.Count;

        /// <summary>시스템을 실행 순서의 마지막에 등록합니다. Tick 콜백 안에서 호출하면 현재 Tick이 끝난 뒤 반영됩니다.</summary>
        /// <returns>새 등록 요청이 받아들여졌으면 true입니다.</returns>
        public bool Register(ISimulationSystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (!isInvokingSystems)
            {
                if (systems.Contains(system))
                    return false;
                systems.Add(system);
                return true;
            }

            if (pendingRemoves.Remove(system))
                return true;
            if (systems.Contains(system) || pendingAdds.Contains(system))
                return false;

            pendingAdds.Add(system);
            return true;
        }

        /// <summary>시스템 등록을 해제합니다. Tick 콜백 안에서 호출하면 현재 Tick이 끝난 뒤 반영됩니다.</summary>
        /// <returns>등록 해제 요청이 받아들여졌으면 true입니다.</returns>
        public bool Unregister(ISimulationSystem system)
        {
            if (system == null)
                return false;

            if (!isInvokingSystems)
                return systems.Remove(system);
            if (pendingAdds.Remove(system))
                return true;
            if (!systems.Contains(system) || pendingRemoves.Contains(system))
                return false;

            pendingRemoves.Add(system);
            return true;
        }

        /// <summary>다음 Reset부터 사용할 설정을 지정합니다.</summary>
        public void Configure(SimulationConfig value) => config = value;

        /// <summary>설정을 교체하고 지정한 틱부터 시뮬레이션을 다시 시작합니다.</summary>
        public void Reset(SimulationConfig value, int startTick = 0)
        {
            Configure(value);
            Reset(startTick);
        }

        /// <summary>난수 상태와 시스템 상태를 초기화하고 지정한 틱부터 다시 시작합니다.</summary>
        public void Reset(int startTick = 0)
        {
            Tick = startTick;
            Random = new DetRandom(config.Seed);
            IsRunning = true;
            InvokeSystems(static (system, simulation) => system.OnSimulationReset(simulation));
        }

        /// <summary>이후 Step 호출이 틱을 진행하지 않도록 정지합니다.</summary>
        public void Stop() => IsRunning = false;

        /// <summary>BeforeTick, SimulateTick 순서로 한 틱을 실행합니다.</summary>
        public void Step()
        {
            if (!IsRunning)
                return;

            isInvokingSystems = true;
            try
            {
                InvokeBeforeTick();
                InvokeSimulationTick();
                Tick++;
            }
            finally
            {
                isInvokingSystems = false;
                ApplyPendingChanges();
            }
        }

        /// <summary>지정한 수만큼 틱을 실행합니다. 실행 중 Stop되면 즉시 중단합니다.</summary>
        public void Step(int count)
        {
            for (int i = 0; i < count && IsRunning; i++)
                Step();
        }

        /// <summary>현재 틱의 명령을 적용한 뒤 시스템 Tick을 실행합니다.</summary>
        public void Step<TCommand>(SimulationCommandQueue<TCommand> commandQueue, Action<TCommand> applyCommand)
            where TCommand : struct
        {
            if (!IsRunning)
                return;
            if (commandQueue == null)
                throw new ArgumentNullException(nameof(commandQueue));
            if (applyCommand == null)
                throw new ArgumentNullException(nameof(applyCommand));

            isInvokingSystems = true;
            try
            {
                InvokeBeforeTick();

                ReadOnlySpan<TCommand> commands = commandQueue.GetCommands(Tick);
                for (int i = 0; i < commands.Length; i++)
                    applyCommand(commands[i]);

                InvokeSimulationTick();
                Tick++;
            }
            finally
            {
                isInvokingSystems = false;
                ApplyPendingChanges();
            }
        }

        /// <summary>현재 틱과 난수 상태를 포함한 재현성 확인용 해시를 만듭니다.</summary>
        public ulong ComputeStateHash(ulong customState = 0) =>
            DeterministicHasher.HashSimulationState(Tick, Random.State, customState);

        /// <summary>시뮬레이션 난수 상태를 사용하는 범용 난수 소스를 만듭니다.</summary>
        public DetRandomSource CreateRandomSource() => new(Random);

        /// <summary>범용 난수 소스에서 변경된 상태를 시뮬레이션에 다시 반영합니다.</summary>
        public void SyncRandom(DetRandomSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            Random = source.Random;
        }

        private void InvokeBeforeTick()
        {
            for (int i = 0; i < systems.Count; i++)
                systems[i].BeforeTick(this);
        }

        private void InvokeSimulationTick()
        {
            for (int i = 0; i < systems.Count; i++)
                systems[i].SimulateTick(this);
        }

        private void InvokeSystems(Action<ISimulationSystem, DeterministicSimulator> callback)
        {
            isInvokingSystems = true;
            try
            {
                for (int i = 0; i < systems.Count; i++)
                    callback(systems[i], this);
            }
            finally
            {
                isInvokingSystems = false;
                ApplyPendingChanges();
            }
        }

        private void ApplyPendingChanges()
        {
            // 콜백 중 목록을 직접 바꾸면 실행 순서가 달라질 수 있어 Tick 경계에서 한 번에 반영합니다.
            for (int i = 0; i < pendingRemoves.Count; i++)
                systems.Remove(pendingRemoves[i]);
            for (int i = 0; i < pendingAdds.Count; i++)
                systems.Add(pendingAdds[i]);

            pendingRemoves.Clear();
            pendingAdds.Clear();
        }
    }
}
