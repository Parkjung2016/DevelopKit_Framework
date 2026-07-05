namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    public static class FixedMath
    {
        public static readonly Fixed64 Pi = Fixed64.FromRaw(13493037705L);
        public static readonly Fixed64 Deg2Rad = Pi / Fixed64.FromInt(180);
        public static Fixed64 Abs(Fixed64 value) => value.Raw < 0 ? new Fixed64(-value.Raw) : value;

        public static Fixed64 Min(Fixed64 a, Fixed64 b) => a.Raw <= b.Raw ? a : b;

        public static Fixed64 Max(Fixed64 a, Fixed64 b) => a.Raw >= b.Raw ? a : b;

        public static Fixed64 Clamp(Fixed64 value, Fixed64 min, Fixed64 max)
        {
            if (value.Raw < min.Raw)
                return min;
            return value.Raw > max.Raw ? max : value;
        }

        public static Fixed64 Lerp(Fixed64 a, Fixed64 b, Fixed64 t)
        {
            t = Clamp(t, Fixed64.Zero, Fixed64.One);
            return a + (b - a) * t;
        }

        public static Fixed64 Sign(Fixed64 value)
        {
            if (value.Raw > 0)
                return Fixed64.One;
            if (value.Raw < 0)
                return -Fixed64.One;
            return Fixed64.Zero;
        }

        public static Fixed64 Sqrt(Fixed64 value)
        {
            if (value.Raw <= 0)
                return Fixed64.Zero;

            Fixed64 guess = value.Raw >= Fixed64.OneRaw ? value : Fixed64.One;
            Fixed64 half = Fixed64.Half;
            for (int i = 0; i < 12; i++)
            {
                if (guess.Raw == 0)
                    break;

                guess = (guess + value / guess) * half;
            }

            return guess;
        }

        /// <summary>각도(degree) 기준 Sin. LUT 기반이라 플랫폼 간 결과가 동일합니다.</summary>
        public static Fixed64 Sin(Fixed64 degrees) => SinCosLookup.Sin(NormalizeDegrees(degrees));

        /// <summary>각도(degree) 기준 Cos.</summary>
        public static Fixed64 Cos(Fixed64 degrees) => SinCosLookup.Cos(NormalizeDegrees(degrees));

        private static Fixed64 NormalizeDegrees(Fixed64 degrees)
        {
            Fixed64 normalized = degrees % Fixed64.FromInt(360);
            if (normalized.Raw < 0)
                normalized += Fixed64.FromInt(360);
            return normalized;
        }

        private static class SinCosLookup
        {
            private const int TableSize = 360;
            private static readonly Fixed64[] sinTable = BuildSinTable();

            public static Fixed64 Sin(Fixed64 degrees)
            {
                int index = degrees.ToIntFloor() % TableSize;
                if (index < 0)
                    index += TableSize;
                return sinTable[index];
            }

            public static Fixed64 Cos(Fixed64 degrees)
            {
                int index = (degrees.ToIntFloor() + 90) % TableSize;
                if (index < 0)
                    index += TableSize;
                return sinTable[index];
            }

            private static Fixed64[] BuildSinTable()
            {
                Fixed64[] table = new Fixed64[TableSize];
                for (int i = 0; i < TableSize; i++)
                    table[i] = TaylorSin(Fixed64.FromInt(i) * Deg2Rad);
                return table;
            }

            private static Fixed64 TaylorSin(Fixed64 radians)
            {
                Fixed64 x = radians;
                Fixed64 x2 = x * x;
                Fixed64 term = x;
                Fixed64 sum = term;

                term = term * -x2 / Fixed64.FromInt(6);
                sum += term;
                term = term * -x2 / Fixed64.FromInt(20);
                sum += term;
                term = term * -x2 / Fixed64.FromInt(42);
                sum += term;

                return sum;
            }
        }
    }
}
