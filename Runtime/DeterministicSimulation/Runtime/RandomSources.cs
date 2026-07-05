using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public static class RandomSources
    {
        public static IRandomSource System(Random random = null) => new SystemRandomSource(random);

        public static IRandomSource Deterministic(ulong seed) => new DetRandomSource(seed);

        public static IRandomSource FromDetRandom(DetRandom random) => new DetRandomSource(random);
    }
}
