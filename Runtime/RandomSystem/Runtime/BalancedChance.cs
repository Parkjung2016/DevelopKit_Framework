using System;

namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    /// <summary>누적 성공 횟수가 목표 확률에서 1회 이상 벗어나지 않게 보정합니다.</summary>
    public sealed class BalancedChance
    {
        private double progress;

        public BalancedChance(IRandomSource random = null)
        {
            progress = (random ?? RandomProvider.Shared).NextDouble();
        }

        public double Progress => progress;

        public bool Roll(double probability)
        {
            if (double.IsNaN(probability) || probability < 0d || probability > 1d)
                throw new ArgumentOutOfRangeException(nameof(probability));
            if (probability <= 0d)
                return false;
            if (probability >= 1d)
                return true;

            progress += probability;
            if (progress < 1d)
                return false;

            progress -= 1d;
            return true;
        }

        public void Reset(IRandomSource random = null) =>
            progress = (random ?? RandomProvider.Shared).NextDouble();
    }
}
