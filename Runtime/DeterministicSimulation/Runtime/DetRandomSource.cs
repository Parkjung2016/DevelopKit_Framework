using System;
using PJDev.DevelopKit.Framework.RandomSystem.Runtime;

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

        public uint NextUInt() => random.NextUInt();

        public int NextInt(int minInclusive, int maxExclusive) =>
            random.NextInt(minInclusive, maxExclusive);

        public float NextFloat() => (random.NextUInt() >> 8) * (1f / 16777216f);

        public double NextDouble()
        {
            ulong upper = random.NextUInt() >> 5;
            ulong lower = random.NextUInt() >> 6;
            return ((upper << 26) + lower) * (1.0 / 9007199254740992.0);
        }
    }
}
