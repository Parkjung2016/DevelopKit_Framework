namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public interface ISimulationSystem
    {
        void OnSimulationReset(DeterministicSimulator simulation);

        void BeforeTick(DeterministicSimulator simulation);

        void SimulateTick(DeterministicSimulator simulation);
    }
}
