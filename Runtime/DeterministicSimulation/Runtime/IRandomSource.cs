using System;

namespace PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime
{
    /// <summary>프레임워크 전역에서 쓰는 난수 소스. <see cref="System.Random"/>·<see cref="DetRandom"/>을 동일 API로 사용합니다.</summary>
    public interface IRandomSource
    {
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>[0, 1) 구간의 실수. 가중치 추첨 등에 사용합니다.</summary>
        double NextDouble();
    }
}
