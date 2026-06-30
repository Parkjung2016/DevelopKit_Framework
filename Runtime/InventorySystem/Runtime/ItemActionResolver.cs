using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class ItemActionResolver : IItemActionResolver
    {
        private readonly Dictionary<int, IItemUseHandler> handlersByItemId = new();

        public void Register(int itemId, IItemUseHandler handler)
        {
            if (itemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(itemId));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            handlersByItemId[itemId] = handler;
        }

        public bool Unregister(int itemId) => handlersByItemId.Remove(itemId);

        public void Clear() => handlersByItemId.Clear();

        public bool TryResolve(int itemId, in ItemDefinition definition, out IItemUseHandler handler) =>
            handlersByItemId.TryGetValue(itemId, out handler);
    }
}
