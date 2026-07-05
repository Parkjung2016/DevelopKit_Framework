using PJDev.DevelopKit.Framework.SocketSystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>비주얼 에셋을 <see cref="ISocketItem"/>으로 생성/해제합니다.</summary>
    public interface IEquipmentVisualSpawner
    {
        void Spawn(in EquipmentVisualSpawnRequest request, EquipmentVisualSpawnCompletedHandler OnSpawnCompleted);

        void Release(ISocketItem socketItem);
    }

    public sealed class NullEquipmentVisualSpawner : IEquipmentVisualSpawner
    {
        public static readonly NullEquipmentVisualSpawner Instance = new();

        public void Spawn(in EquipmentVisualSpawnRequest request, EquipmentVisualSpawnCompletedHandler OnSpawnCompleted) =>
            OnSpawnCompleted?.Invoke(null);

        public void Release(ISocketItem socketItem)
        {
        }
    }
}
