using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public interface IItemCatalog : IItemDatabase
    {
        IReadOnlyCollection<int> ItemIds { get; }
        bool TryGetEntry(int itemId, out ItemCatalogEntry entry);
        void FindByTag(string tag, List<ItemCatalogEntry> results);
    }
}
