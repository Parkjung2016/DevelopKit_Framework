using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>PCG32 기반 결정론적 난수 생성기. 동일 seed → 동일 시퀀스.</summary>
    public struct DetRandom : IEquatable<DetRandom>
    {
        private ulong state;

        public DetRandom(ulong seed)
        {
            state = seed == 0 ? 0x853C49E6748FEA9BUL : unchecked(seed * 6364136223846793005UL + 1442695040888963407UL);
        }

        public ulong State => state;

        public DetRandom WithState(ulong newState) => new DetRandom { state = newState == 0 ? 1UL : newState };

        public uint NextUInt()
        {
            ulong oldState = state;
            state = oldState * 6364136223846793005UL + 1442695040888963407UL;

            uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rot = (int)(oldState >> 59);
            return (xorshifted >> rot) | (xorshifted << (-rot & 31));
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        public Fixed64 NextFixed01()
        {
            uint value = NextUInt();
            return Fixed64.FromRaw((long)((ulong)value << 33) >> 1);
        }

        public Fixed64 NextFixed(Fixed64 minInclusive, Fixed64 maxExclusive) =>
            minInclusive + (maxExclusive - minInclusive) * NextFixed01();

        public bool NextBool() => (NextUInt() & 1) == 0;

        public bool Equals(DetRandom other) => state == other.state;

        public override bool Equals(object obj) => obj is DetRandom other && Equals(other);

        public override int GetHashCode() => state.GetHashCode();
    }
}
