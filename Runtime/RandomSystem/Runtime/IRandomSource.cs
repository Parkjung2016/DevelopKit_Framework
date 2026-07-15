namespace PJDev.DevelopKit.Framework.RandomSystem.Runtime
{
    /// <summary>난수가 필요한 시스템이 구현체와 관계없이 사용하는 공통 인터페이스입니다.</summary>
    public interface IRandomSource
    {
        uint NextUInt();
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        double NextDouble();
    }
}
