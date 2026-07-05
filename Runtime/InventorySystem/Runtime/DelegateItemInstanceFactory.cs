using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class DelegateItemInstanceFactory : IItemInstanceFactory
    {
        private readonly Func<int, IItemInstanceData> factory;

        public DelegateItemInstanceFactory(Func<int, IItemInstanceData> factory) =>
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public bool TryCreate(int itemId, out IItemInstanceData data)
        {
            data = factory(itemId);
            return data != null;
        }
    }
}
