namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>시뮬레이션의 틱 속도와 난수 시드를 설정합니다.</summary>
    public readonly struct SimulationConfig
    {
        public SimulationConfig(int tickRate, ulong seed)
        {
            TickRate = tickRate > 0 ? tickRate : 60;
            Seed = seed;
        }

        /// <summary>초당 실행할 시뮬레이션 틱 수입니다.</summary>
        public int TickRate { get; }

        /// <summary>결정론 난수 생성에 사용할 초기값입니다.</summary>
        public ulong Seed { get; }

        public static SimulationConfig Default => new(60, 1);
    }
}
