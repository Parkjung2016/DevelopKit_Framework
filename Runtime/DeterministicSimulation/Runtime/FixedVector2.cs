using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public readonly struct FixedVector2 : IEquatable<FixedVector2>
    {
        public Fixed64 X { get; }
        public Fixed64 Y { get; }

        public static readonly FixedVector2 Zero = new(Fixed64.Zero, Fixed64.Zero);
        public static readonly FixedVector2 One = new(Fixed64.One, Fixed64.One);

        public FixedVector2(Fixed64 x, Fixed64 y)
        {
            X = x;
            Y = y;
        }

        public Fixed64 SqrMagnitude => X * X + Y * Y;

        public Fixed64 Magnitude => FixedMath.Sqrt(SqrMagnitude);

        public FixedVector2 Normalized
        {
            get
            {
                Fixed64 magnitude = Magnitude;
                if (magnitude.Raw == 0)
                    return Zero;
                return this / magnitude;
            }
        }

        public static FixedVector2 operator +(FixedVector2 a, FixedVector2 b) => new(a.X + b.X, a.Y + b.Y);

        public static FixedVector2 operator -(FixedVector2 a, FixedVector2 b) => new(a.X - b.X, a.Y - b.Y);

        public static FixedVector2 operator -(FixedVector2 value) => new(-value.X, -value.Y);

        public static FixedVector2 operator *(FixedVector2 value, Fixed64 scale) => new(value.X * scale, value.Y * scale);

        public static FixedVector2 operator /(FixedVector2 value, Fixed64 scale) => new(value.X / scale, value.Y / scale);

        public static Fixed64 Dot(FixedVector2 a, FixedVector2 b) => a.X * b.X + a.Y * b.Y;

        public static Fixed64 Distance(FixedVector2 a, FixedVector2 b) => (a - b).Magnitude;

        public bool Equals(FixedVector2 other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object obj) => obj is FixedVector2 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"({X}, {Y})";
    }
}
