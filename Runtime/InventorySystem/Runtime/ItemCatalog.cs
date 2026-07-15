using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.Shared.Runtime;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// 프로젝트 전역에서 사용하는 아이템 카탈로그입니다. 컨테이너마다 별도 데이터베이스를 지정하지 않아도 같은 정의를 공유할 수 있습니다.
    /// </summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class ItemCatalog
    {
        public static bool IsReady => GlobalRegistry<IItemCatalog>.IsReady;

        public static IItemCatalog Current =>
            GlobalRegistry<IItemCatalog>.ResolveOrDefault(null, NullItemCatalog.Instance);

        public static void Set(IItemCatalog catalog) => GlobalRegistry<IItemCatalog>.Set(catalog);

        public static void Set(IItemDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            Set(database as IItemCatalog ?? new ItemDatabaseCatalogAdapter(database));
        }

        public static void Clear() => GlobalRegistry<IItemCatalog>.Clear();

        public static IItemDatabase Resolve(IItemDatabase database = null) =>
            database ?? Current;

        public static IItemCatalog ResolveCatalog(IItemCatalog catalog = null) =>
            catalog ?? Current;

        public static bool TryGetDefinition(int itemId, out ItemDefinition definition) =>
            Current.TryGetDefinition(itemId, out definition);
    }

    internal sealed class NullItemCatalog : IItemCatalog
    {
        public static readonly NullItemCatalog Instance = new();

        public IReadOnlyCollection<int> ItemIds => Array.Empty<int>();

        public bool TryGetDefinition(int itemId, out ItemDefinition definition)
        {
            definition = default;
            return false;
        }

        public bool TryGetEntry(int itemId, out ItemCatalogEntry entry)
        {
            entry = default;
            return false;
        }

        public void FindByTag(string tag, List<ItemCatalogEntry> results)
        {
            results?.Clear();
        }
    }

    internal sealed class ItemDatabaseCatalogAdapter : IItemCatalog
    {
        private readonly IItemDatabase database;

        public ItemDatabaseCatalogAdapter(IItemDatabase database) =>
            this.database = database ?? throw new ArgumentNullException(nameof(database));

        public IReadOnlyCollection<int> ItemIds => Array.Empty<int>();

        public bool TryGetDefinition(int itemId, out ItemDefinition definition) =>
            database.TryGetDefinition(itemId, out definition);

        public bool TryGetEntry(int itemId, out ItemCatalogEntry entry)
        {
            if (!database.TryGetDefinition(itemId, out ItemDefinition definition))
            {
                entry = default;
                return false;
            }

            entry = new ItemCatalogEntry(definition, itemId.ToString(), string.Empty, string.Empty, Array.Empty<string>());
            return true;
        }

        public void FindByTag(string tag, List<ItemCatalogEntry> results) => results?.Clear();
    }
}
