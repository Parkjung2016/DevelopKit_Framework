using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEngine;

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
            if (!prefabsByKey.TryGetValue(request.AssetKey, out GameObject prefab) || prefab == null)
            {
                OnSpawnCompleted?.Invoke(null);
                return;
            }

            OnSpawnCompleted?.Invoke(SocketItemUtility.FromGameObject(prefab));
        }

        public void Release(ISocketItem socketItem) => SocketItemUtility.ReleaseDestroy(socketItem);
    }
}
