using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 스탯 정의를 이름으로 조회할 수 있는 데이터 원본입니다.
    /// </summary>
    public interface IStatCatalog
    {
        IReadOnlyList<StatDefinition> Definitions { get; }

        bool TryGetDefinition(string statName, out StatDefinition definition);
    }
}