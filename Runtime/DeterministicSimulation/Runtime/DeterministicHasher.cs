using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public static class DeterministicHasher
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Combine(ulong hash, int value) => Combine(hash, unchecked((ulong)(uint)value));

        public static ulong Combine(ulong hash, long value) => Combine(hash, unchecked((ulong)value));

        public static ulong Combine(ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value;
                hash *= Prime;
                return hash;
            }
        }

        public static ulong Hash(ReadOnlySpan<byte> bytes)
        {
            unchecked
            {
                ulong hash = OffsetBasis;
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= Prime;
                }

                return hash;
            }
        }

        public static ulong HashSimulationState(int tick, ulong randomState, ulong customState = 0)
        {
            unchecked
            {
                ulong hash = OffsetBasis;
                hash = Combine(hash, tick);
                hash = Combine(hash, randomState);
                hash = Combine(hash, customState);
                return hash;
            }
        }
    }
}
