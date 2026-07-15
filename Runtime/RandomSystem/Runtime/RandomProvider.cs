using System;
using System.Threading;

namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    /// <summary>공유 난수 생성기와 seed 기반 생성기를 제공합니다.</summary>
    public static class RandomProvider
    {
        private static long seedCounter = DateTime.UtcNow.Ticks;

        [ThreadStatic] private static RandomGenerator shared;

        public static RandomGenerator Shared => shared ??= Create();

        public static RandomGenerator Create() => new(CreateSeed());

        public static RandomGenerator Create(ulong seed, ulong stream = 1442695040888963407UL) =>
            new(seed, stream);

        private static ulong CreateSeed()
        {
            ulong value = unchecked((ulong)Interlocked.Increment(ref seedCounter));
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
