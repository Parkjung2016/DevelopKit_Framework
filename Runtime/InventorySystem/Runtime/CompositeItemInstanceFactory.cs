using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>여러 <see cref="IItemInstanceFactory"/>를 순서대로 시도합니다.</summary>
    public sealed class CompositeItemInstanceFactory : IItemInstanceFactory
    {
        private readonly IItemInstanceFactory[] factories;

        public CompositeItemInstanceFactory(params IItemInstanceFactory[] factories)
        {
            if (factories == null || factories.Length == 0)
                throw new ArgumentException("At least one factory is required.", nameof(factories));

            this.factories = factories;
        }

        public bool TryCreate(int itemId, out IItemInstanceData data)
        {
            for (int i = 0; i < factories.Length; i++)
            {
                if (factories[i] != null && factories[i].TryCreate(itemId, out data))
                    return true;
            }

            data = null;
            return false;
        }
    }
}
