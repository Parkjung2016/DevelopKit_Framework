namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public readonly struct SimulationConfig
    {
        public SimulationConfig(int tickRate, ulong seed)
        {
            TickRate = tickRate > 0 ? tickRate : 60;
            Seed = seed;
        }

        /// <summary>초당 시뮬레이션 틱 수.</summary>
        public int TickRate { get; }

        public ulong Seed { get; }

        public static SimulationConfig Default => new(60, 1);
    }
}
