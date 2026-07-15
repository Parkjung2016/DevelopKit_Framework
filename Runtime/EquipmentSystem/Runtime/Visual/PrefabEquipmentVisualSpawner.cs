using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>AssetKey에 연결된 프리팹을 생성하고 장비 소켓에 붙일 항목으로 반환합니다.</summary>
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

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            OnSpawnCompleted?.Invoke(SocketItemUtility.FromGameObject(instance));
        }

        public void Release(ISocketItem socketItem) => SocketItemUtility.ReleaseDestroy(socketItem);
    }
}
