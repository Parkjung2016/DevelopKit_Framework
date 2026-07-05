using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>Q32.32 고정소수점. 플랫폼·CPU에 관계없이 동일한 연산 결과를 보장합니다.</summary>
    public readonly struct Fixed64 : IEquatable<Fixed64>, IComparable<Fixed64>
    {
        public const int FractionalBits = 32;
        public const long OneRaw = 1L << FractionalBits;

        public long Raw { get; }

        public static readonly Fixed64 Zero = new(0);
        public static readonly Fixed64 One = new(OneRaw);
        public static readonly Fixed64 Half = new(OneRaw >> 1);
        public static readonly Fixed64 Epsilon = new(1);

        public Fixed64(long raw) => Raw = raw;

        public static Fixed64 FromInt(int value) => new((long)value << FractionalBits);

        public static Fixed64 FromRaw(long raw) => new(raw);

        /// <summary>에디터·디버그 전용. 런타임 결정론 경로에서는 사용하지 마세요.</summary>
        public static Fixed64 FromFloat(float value) => new((long)(value * OneRaw));

        public float ToFloat() => (float)Raw / OneRaw;

        public int ToIntFloor() => (int)(Raw >> FractionalBits);

        public int ToIntRound() => (int)((Raw + (OneRaw >> 1)) >> FractionalBits);

        public static Fixed64 operator +(Fixed64 a, Fixed64 b) => new(a.Raw + b.Raw);

        public static Fixed64 operator -(Fixed64 a, Fixed64 b) => new(a.Raw - b.Raw);

        public static Fixed64 operator -(Fixed64 a) => new(-a.Raw);

        public static Fixed64 operator *(Fixed64 a, Fixed64 b) => new(MultiplyRaw(a.Raw, b.Raw));

        public static Fixed64 operator /(Fixed64 a, Fixed64 b) => new(DivideRaw(a.Raw, b.Raw));

        public static Fixed64 operator %(Fixed64 a, Fixed64 b) => new(a.Raw % b.Raw);

        public static bool operator ==(Fixed64 a, Fixed64 b) => a.Raw == b.Raw;

        public static bool operator !=(Fixed64 a, Fixed64 b) => a.Raw != b.Raw;

        public static bool operator <(Fixed64 a, Fixed64 b) => a.Raw < b.Raw;

        public static bool operator >(Fixed64 a, Fixed64 b) => a.Raw > b.Raw;

        public static bool operator <=(Fixed64 a, Fixed64 b) => a.Raw <= b.Raw;

        public static bool operator >=(Fixed64 a, Fixed64 b) => a.Raw >= b.Raw;

        public static Fixed64 operator >>(Fixed64 value, int bits) => new(value.Raw >> bits);

        public static Fixed64 operator <<(Fixed64 value, int bits) => new(value.Raw << bits);

        public int CompareTo(Fixed64 other) => Raw.CompareTo(other.Raw);

        public bool Equals(Fixed64 other) => Raw == other.Raw;

        public override bool Equals(object obj) => obj is Fixed64 other && Equals(other);

        public override int GetHashCode() => Raw.GetHashCode();

        public override string ToString() => ToFloat().ToString("0.####");

        internal static long MultiplyRaw(long a, long b)
        {
            unchecked
            {
                bool negative = (a ^ b) < 0;
                if (a < 0)
                    a = -a;
                if (b < 0)
                    b = -b;

                long xl = a & 0x00000000FFFFFFFFL;
                long xh = a >> 32;
                long yl = b & 0x00000000FFFFFFFFL;
                long yh = b >> 32;

                long hl = xh * yl;
                long lh = xl * yh;
                long ll = xl * yl;
                long hh = xh * yh;

                long llt = ll >> 32;
                long hlt = hl >> 32;
                long lht = lh >> 32;

                long lhm = (hl & 0x00000000FFFFFFFFL) + (lh & 0x00000000FFFFFFFFL) + llt;
                long m = hlt + lht + (lhm >> 32);

                long result = (hh << 32) + (m << 32) + lhm;
                return negative ? -result : result;
            }
        }

        internal static long DivideRaw(long a, long b)
        {
            if (b == 0)
                throw new DivideByZeroException();

            unchecked
            {
                bool negative = (a ^ b) < 0;
                if (a < 0)
                    a = -a;
                if (b < 0)
                    b = -b;

                ulong dividend = (ulong)a;
                ulong divisor = (ulong)b;

                if (dividend <= 0xFFFFFFFFUL)
                {
                    long quotient = (long)((dividend << 32) / divisor);
                    return negative ? -quotient : quotient;
                }

                int shift = 0;
                while (dividend > 0xFFFFFFFFUL)
                {
                    dividend >>= 1;
                    shift++;
                }

                long scaled = (long)((dividend << 32) / divisor);
                long result = scaled << shift;
                return negative ? -result : result;
            }
        }
    }
}
