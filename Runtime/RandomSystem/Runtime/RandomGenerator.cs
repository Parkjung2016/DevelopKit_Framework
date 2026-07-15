using System;

namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    /// <summary>빠르고 재현 가능한 PCG32 난수 생성기입니다.</summary>
    public sealed class RandomGenerator : IRandomSource
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultStream = 1442695040888963407UL;

        private ulong state;
        private ulong stream;

        public RandomGenerator(ulong seed, ulong stream = DefaultStream)
        {
            Seed(seed, stream);
        }

        public RandomSnapshot Snapshot => new(state, stream);

        public void Seed(ulong seed, ulong streamValue = DefaultStream)
        {
            state = 0UL;
            stream = (streamValue << 1) | 1UL;
            NextUInt();
            state = unchecked(state + seed);
            NextUInt();
        }

        public void Restore(RandomSnapshot snapshot)
        {
            state = snapshot.State;
            stream = snapshot.Stream | 1UL;
        }

        public uint NextUInt()
        {
            ulong previous = state;
            state = unchecked(previous * Multiplier + stream);
            uint value = (uint)(((previous >> 18) ^ previous) >> 27);
            int rotation = (int)(previous >> 59);
            return (value >> rotation) | (value << ((-rotation) & 31));
        }

        public ulong NextULong() => ((ulong)NextUInt() << 32) | NextUInt();

        public int NextInt(int minInclusive, int maxExclusive)
        {
            long span = (long)maxExclusive - minInclusive;
            if (span <= 0L)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");

            uint range = (uint)span;
            uint threshold = unchecked(0U - range) % range;
            uint value;
            do
            {
                value = NextUInt();
            }
            while (value < threshold);

            return (int)(minInclusive + (long)(value % range));
        }

        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public double NextDouble()
        {
            ulong upper = NextUInt() >> 5;
            ulong lower = NextUInt() >> 6;
            return ((upper << 26) + lower) * (1.0 / 9007199254740992.0);
        }

        public bool NextBool() => (NextUInt() & 1U) != 0U;

        public bool Chance(double probability)
        {
            if (double.IsNaN(probability) || probability < 0d || probability > 1d)
                throw new ArgumentOutOfRangeException(nameof(probability));

            return probability >= 1d || probability > 0d && NextDouble() < probability;
        }
    }
}
