using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_StatDatabase", menuName = "SO/StatSystem/StatDatabase")]
    public class StatDatabaseSO : ScriptableObject, IStatCatalog
    {
        [field: SerializeField] public StatSO[] Stats { get; set; } = System.Array.Empty<StatSO>();

        private readonly Dictionary<string, StatDefinition> definitionCache = new();
        private readonly Dictionary<string, StatSO> statCache = new();
        private readonly Dictionary<string, StatCatalogEntry> entryCache = new();

        public IReadOnlyCollection<string> StatNames => statCache.Keys;

        private void OnEnable() => RebuildCache();

        private void OnValidate() => RebuildCache();

        public void RebuildCache()
        {
            definitionCache.Clear();
            statCache.Clear();
            entryCache.Clear();

            if (Stats == null)
                return;

            for (int i = 0; i < Stats.Length; i++)
            {
                StatSO stat = Stats[i];
                if (stat == null || string.IsNullOrEmpty(stat.StatName))
                    continue;

                if (statCache.ContainsKey(stat.StatName))
                {
                    CDebug.LogWarning($"StatDatabaseSO : duplicate stat name {stat.StatName} in {name}.");
                    continue;
                }

                StatDefinition definition = stat.ToDefinition();
                statCache.Add(stat.StatName, stat);
                definitionCache.Add(stat.StatName, definition);
                entryCache.Add(stat.StatName, StatCatalogEntry.FromDefinition(definition));
            }
        }

        public bool TryGetDefinition(string statName, out StatDefinition definition) =>
            definitionCache.TryGetValue(statName, out definition);

        public bool TryGetEntry(string statName, out StatCatalogEntry entry) =>
            entryCache.TryGetValue(statName, out entry);

        public bool TryGetStat(string statName, out StatSO stat) =>
            statCache.TryGetValue(statName, out stat);
    }
}
