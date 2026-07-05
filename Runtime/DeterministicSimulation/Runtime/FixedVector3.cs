using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public readonly struct FixedVector3 : IEquatable<FixedVector3>
    {
        public Fixed64 X { get; }
        public Fixed64 Y { get; }
        public Fixed64 Z { get; }

        public static readonly FixedVector3 Zero = new(Fixed64.Zero, Fixed64.Zero, Fixed64.Zero);
        public static readonly FixedVector3 One = new(Fixed64.One, Fixed64.One, Fixed64.One);

        public FixedVector3(Fixed64 x, Fixed64 y, Fixed64 z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Fixed64 SqrMagnitude => X * X + Y * Y + Z * Z;

        public Fixed64 Magnitude => FixedMath.Sqrt(SqrMagnitude);

        public FixedVector3 Normalized
        {
            get
            {
                Fixed64 magnitude = Magnitude;
                if (magnitude.Raw == 0)
                    return Zero;
                return this / magnitude;
            }
        }

        public static FixedVector3 operator +(FixedVector3 a, FixedVector3 b) =>
            new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static FixedVector3 operator -(FixedVector3 a, FixedVector3 b) =>
            new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static FixedVector3 operator -(FixedVector3 value) => new(-value.X, -value.Y, -value.Z);

        public static FixedVector3 operator *(FixedVector3 value, Fixed64 scale) =>
            new(value.X * scale, value.Y * scale, value.Z * scale);

        public static FixedVector3 operator /(FixedVector3 value, Fixed64 scale) =>
            new(value.X / scale, value.Y / scale, value.Z / scale);

        public static Fixed64 Dot(FixedVector3 a, FixedVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static FixedVector3 Cross(FixedVector3 a, FixedVector3 b) =>
            new(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);

        public static Fixed64 Distance(FixedVector3 a, FixedVector3 b) => (a - b).Magnitude;

        public bool Equals(FixedVector3 other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is FixedVector3 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
