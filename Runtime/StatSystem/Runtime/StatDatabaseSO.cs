using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_StatDatabase", menuName = "PJDev/Stat System/Stat Database")]
    public sealed class StatDatabaseSO : ScriptableObject, IStatCatalog
    {
        [SerializeField]
        private StatSO[] stats = Array.Empty<StatSO>();

        private readonly List<StatDefinition> definitions = new();
        private readonly Dictionary<string, StatDefinition> definitionsByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, StatSO> assetsByName = new(StringComparer.Ordinal);

        public StatSO[] Stats => stats;
        public IReadOnlyList<StatDefinition> Definitions => definitions;

        private void OnEnable() => RebuildCache();

#if UNITY_EDITOR
        private void OnValidate() => RebuildCache();
#endif

        public void RebuildCache()
        {
            definitions.Clear();
            definitionsByName.Clear();
            assetsByName.Clear();

            for (int i = 0; i < stats.Length; i++)
            {
                StatSO statAsset = stats[i];
                if (statAsset == null)
                    continue;

                StatDefinition definition = statAsset.CreateDefinition();
                if (!definition.IsValid || definitionsByName.ContainsKey(definition.StatName))
                    continue;

                definitions.Add(definition);
                definitionsByName.Add(definition.StatName, definition);
                assetsByName.Add(definition.StatName, statAsset);
            }
        }

        public bool TryGetDefinition(string statName, out StatDefinition definition)
        {
            if (!string.IsNullOrEmpty(statName))
                return definitionsByName.TryGetValue(statName, out definition);

            definition = default;
            return false;
        }

        public bool TryGetAsset(string statName, out StatSO statAsset)
        {
            if (!string.IsNullOrEmpty(statName))
                return assetsByName.TryGetValue(statName, out statAsset);

            statAsset = null;
            return false;
        }
    }
}