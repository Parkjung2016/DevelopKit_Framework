using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random;

        public SystemRandomSource(Random random = null) => this.random = random ?? new Random();

        public int NextInt(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);

        public double NextDouble() => random.NextDouble();
    }
}
