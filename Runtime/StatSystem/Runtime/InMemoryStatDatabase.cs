using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 서버 데이터나 테스트 데이터처럼 런타임에 구성하는 스탯 카탈로그입니다.
    /// </summary>
    public class InMemoryStatDatabase : IStatCatalog
    {
        private readonly List<StatDefinition> definitions = new();
        private readonly Dictionary<StatId, int> indices = new();

        public IReadOnlyList<StatDefinition> Definitions => definitions;

        public void Clear()
        {
            definitions.Clear();
            indices.Clear();
        }

        public bool Register(in StatDefinition definition)
        {
            if (!definition.IsValid)
                return false;

            if (indices.TryGetValue(definition.Id, out int index))
            {
                definitions[index] = definition;
                return false;
            }

            indices.Add(definition.Id, definitions.Count);
            definitions.Add(definition);
            return true;
        }

        public void RegisterRange(IEnumerable<StatDefinition> source)
        {
            if (source == null)
                return;

            foreach (StatDefinition definition in source)
                Register(definition);
        }

        public bool TryGetDefinition(StatId id, out StatDefinition definition)
        {
            if (id.IsValid && indices.TryGetValue(id, out int index))
            {
                definition = definitions[index];
                return true;
            }

            definition = default;
            return false;
        }
    }
}