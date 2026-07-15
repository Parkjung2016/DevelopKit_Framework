using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    [Serializable]
    public sealed class PrefabPoolConfig
    {
        [SerializeField] private GameObject prefab = null;
        [SerializeField, Min(0)] private int prewarmCount = 0;
        [SerializeField, Min(1)] private int maxSize = 128;

        public GameObject Prefab => prefab;
        public int PrewarmCount => prewarmCount;
        public int MaxSize => maxSize;
    }

    [CreateAssetMenu(fileName = "SO_PrefabPoolSettings", menuName = "PJDev/Pool System/Prefab Pool Settings")]
    public sealed class PrefabPoolSettingsSO : ScriptableObject
    {
        [SerializeField] private List<PrefabPoolConfig> pools = new();

        public IReadOnlyList<PrefabPoolConfig> Pools => pools;

        public void Prewarm()
        {
            for (int i = 0; i < pools.Count; i++)
            {
                PrefabPoolConfig config = pools[i];
                if (config?.Prefab != null)
                    PrefabPool.Prewarm(config.Prefab, config.PrewarmCount, config.MaxSize);
            }
        }
    }
}