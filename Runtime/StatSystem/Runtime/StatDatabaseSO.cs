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
        private readonly Dictionary<StatId, StatDefinition> definitionsById = new();
        private readonly Dictionary<StatId, StatSO> assetsById = new();

        public StatSO[] Stats => stats;
        public IReadOnlyList<StatDefinition> Definitions => definitions;

        private void OnEnable() => RebuildCache();

#if UNITY_EDITOR
        private void OnValidate() => RebuildCache();
#endif

        public void RebuildCache()
        {
            definitions.Clear();
            definitionsById.Clear();
            assetsById.Clear();

            for (int i = 0; i < stats.Length; i++)
            {
                StatSO statAsset = stats[i];
                if (statAsset == null)
                    continue;

                StatDefinition definition = statAsset.CreateDefinition();
                if (!definition.IsValid || definitionsById.ContainsKey(definition.Id))
                    continue;

                definitions.Add(definition);
                definitionsById.Add(definition.Id, definition);
                assetsById.Add(definition.Id, statAsset);
            }
        }

        public bool TryGetDefinition(StatId id, out StatDefinition definition)
        {
            if (id.IsValid)
                return definitionsById.TryGetValue(id, out definition);

            definition = default;
            return false;
        }

        public bool TryGetAsset(StatId id, out StatSO statAsset)
        {
            if (id.IsValid)
                return assetsById.TryGetValue(id, out statAsset);

            statAsset = null;
            return false;
        }
    }
}