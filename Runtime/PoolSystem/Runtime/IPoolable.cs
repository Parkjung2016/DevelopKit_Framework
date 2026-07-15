namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    /// <summary>GameObject가 풀에서 나오거나 돌아갈 때 상태를 초기화합니다.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}