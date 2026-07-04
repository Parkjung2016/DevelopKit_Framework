using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>AssetKey → Prefab 매핑으로 동기 스폰합니다. 부착은 <see cref="ObjectSocket.ChangeItem"/>에서 처리합니다.</summary>
    public sealed class PrefabEquipmentVisualSpawner : IEquipmentVisualSpawner
    {
        private readonly IReadOnlyDictionary<string, GameObject> prefabsByKey;

        public PrefabEquipmentVisualSpawner(IReadOnlyDictionary<string, GameObject> prefabsByKey)
        {
            this.prefabsByKey = prefabsByKey ?? throw new ArgumentNullException(nameof(prefabsByKey));
        }

        public void Spawn(in EquipmentVisualSpawnRequest request, EquipmentVisualSpawnCompletedHandler OnSpawnCompleted)
        {
            if (prefabsByKey.TryGetValue(request.Definition.AssetKey, out GameObject prefab) && prefab != null)
                OnSpawnCompleted?.Invoke(Object.Instantiate(prefab));
            else
                OnSpawnCompleted?.Invoke(null);
        }

        public void Release(GameObject instance)
        {
            if (instance != null)
                Object.Destroy(instance);
        }
    }
}
