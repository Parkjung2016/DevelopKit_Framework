using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary><see cref="IItemInstanceFactory"/> 생성 헬퍼입니다.</summary>
    public static class ItemInstanceFactories
    {
        public static IItemInstanceFactory Delegate(Func<int, IItemInstanceData> create) =>
            new DelegateItemInstanceFactory(create);

        public static IItemInstanceFactory Create<T>() where T : class, IItemInstanceData, new() =>
            Delegate(_ => new T());

        public static IItemInstanceFactory Create(Func<IItemInstanceData> create) =>
            Delegate(_ => create());
    }
}
