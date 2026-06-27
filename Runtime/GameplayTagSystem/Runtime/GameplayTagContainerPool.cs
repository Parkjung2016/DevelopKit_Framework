using System;
using UnityEngine.Pool;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary><see cref="GameplayTagContainer"/> 인스턴스 풀입니다.</summary>
    public sealed class GameplayTagContainerPool
    {
        public readonly struct PooledContainer : IDisposable
        {
            public GameplayTagContainer Container => container;

            private readonly GameplayTagContainer container;

            public PooledContainer(GameplayTagContainer container)
            {
                this.container = container;
            }

            public readonly void Dispose()
            {
                Release(container);
            }
        }

        private static readonly ObjectPool<GameplayTagContainer> instance = new
        (
            CreateContainer,
            actionOnRelease: OnReleaseContainer,
            collectionCheck: false
        );

        public static GameplayTagContainer Get()
        {
            return instance.Get();
        }

        public static void Release(GameplayTagContainer container)
        {
            instance.Release(container);
        }

        public static PooledContainer Get(out GameplayTagContainer container)
        {
            container = Get();
            return new(container);
        }

        private static GameplayTagContainer CreateContainer()
        {
            return new();
        }

        private static void OnReleaseContainer(GameplayTagContainer container)
        {
            container.Clear();
        }
    }
}
