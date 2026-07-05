using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public sealed class DetRandomSource : IRandomSource
    {
        private DetRandom random;

        public DetRandomSource(ulong seed) => random = new DetRandom(seed);

        public DetRandomSource(DetRandom random) => this.random = random;

        public DetRandom Random
        {
            get => random;
            set => random = value;
        }

        public int NextInt(int minInclusive, int maxExclusive) =>
            random.NextInt(minInclusive, maxExclusive);

        public double NextDouble() => random.NextUInt() * (1.0 / 4294967296.0);
    }
}
