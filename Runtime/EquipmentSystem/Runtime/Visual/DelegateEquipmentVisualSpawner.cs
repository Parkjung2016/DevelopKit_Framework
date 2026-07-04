using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary><see cref="EquipmentVisualSpawnHandler"/>로 spawner를 구성합니다.</summary>
    public sealed class DelegateEquipmentVisualSpawner : IEquipmentVisualSpawner
    {
        private readonly EquipmentVisualSpawnHandler OnSpawn;
        private readonly EquipmentVisualReleaseHandler OnRelease;

        public DelegateEquipmentVisualSpawner(
            EquipmentVisualSpawnHandler OnSpawn,
            EquipmentVisualReleaseHandler OnRelease = null)
        {
            this.OnSpawn = OnSpawn ?? throw new ArgumentNullException(nameof(OnSpawn));
            this.OnRelease = OnRelease ?? ReleaseDefault;
        }

        public void Spawn(in EquipmentVisualSpawnRequest request, EquipmentVisualSpawnCompletedHandler OnSpawnCompleted) =>
            OnSpawn(request, OnSpawnCompleted);

        public void Release(GameObject instance) => OnRelease(instance);

        private static void ReleaseDefault(GameObject instance)
        {
            if (instance != null)
                Object.Destroy(instance);
        }
    }
}
