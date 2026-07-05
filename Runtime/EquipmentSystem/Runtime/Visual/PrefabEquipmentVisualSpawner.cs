using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
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
            if (!prefabsByKey.TryGetValue(request.Definition.AssetKey, out GameObject prefab) || prefab == null)
            {
                OnSpawnCompleted?.Invoke(null);
                return;
            }

            GameObject instance = Object.Instantiate(prefab);
            ISocketItem socketItem = instance.TryGetComponent(out ISocketItem existing)
                ? existing
                : new GameObjectSocketItem(instance);

            OnSpawnCompleted?.Invoke(socketItem);
        }

        public void Release(ISocketItem socketItem) => SocketItemUtility.ReleaseDestroy(socketItem);
    }
}
