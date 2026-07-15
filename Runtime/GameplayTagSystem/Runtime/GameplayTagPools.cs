using PJDev.DevelopKit.Framework.PoolSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    internal static class GameplayTagPools
    {
        private static readonly Pool<GameplayTagContainer> Containers = new(
            create: static () => new GameplayTagContainer(),
            onReturn: static container => container.Clear(),
            initialCapacity: 1,
            maxSize: 32,
            collectionCheck: true);

        public static PoolLease<GameplayTagContainer> Rent(out GameplayTagContainer container) =>
            Containers.Rent(out container);
    }
}