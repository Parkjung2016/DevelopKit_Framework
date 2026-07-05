using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>Fluent API로 <see cref="IItemInstanceFactory"/>를 조합합니다.</summary>
    public sealed class ItemInstanceFactoryBuilder
    {
        private readonly ItemTypeItemInstanceFactory root = new();

        public static ItemInstanceFactoryBuilder Create() => new();

        public ItemInstanceFactoryBuilder For(ItemType itemType, IItemInstanceFactory factory)
        {
            root.Set(itemType, factory);
            return this;
        }

        public ItemInstanceFactoryBuilder For(ItemType itemType, Func<int, IItemInstanceData> create)
        {
            root.Set(itemType, create);
            return this;
        }

        public ItemInstanceFactoryBuilder For<T>(ItemType itemType) where T : class, IItemInstanceData, new()
        {
            root.Set<T>(itemType);
            return this;
        }

        public ItemInstanceFactoryBuilder For(ItemType itemType, Func<IItemInstanceData> create)
        {
            root.Set(itemType, create);
            return this;
        }

        public ItemInstanceFactoryBuilder SetFallback(IItemInstanceFactory factory)
        {
            root.SetFallback(factory);
            return this;
        }

        public ItemInstanceFactoryBuilder SetFallback(Func<int, IItemInstanceData> create)
        {
            root.SetFallback(create);
            return this;
        }

        public ItemInstanceFactoryBuilder SetFallback<T>() where T : class, IItemInstanceData, new()
        {
            root.SetFallback<T>();
            return this;
        }

        public ItemInstanceFactoryBuilder SetFallback(Func<IItemInstanceData> create)
        {
            root.SetFallback(create);
            return this;
        }

        public ItemInstanceFactoryBuilder SetFallback() => SetFallback<EmptyItemInstanceData>();

        public IItemInstanceFactory Build() => root;
    }
}
