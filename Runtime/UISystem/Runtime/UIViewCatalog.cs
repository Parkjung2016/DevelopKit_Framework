using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>프리팹 뷰를 ID·타입으로 조회하는 카탈로그입니다.</summary>
    [CreateAssetMenu(fileName = "UIViewCatalog", menuName = "PJDev/SO/UI/View Catalog")]
    public sealed class UIViewCatalog : ScriptableObject
    {
        [SerializeField]
        private List<UIViewCatalogEntry> entries = new();

        internal IReadOnlyList<UIViewCatalogEntry> GetEntriesForEditor() => entries;

        private Dictionary<string, UIViewCatalogEntry> entriesById;
        private Dictionary<Type, UIViewCatalogEntry> entriesByType;

        public bool TryGet(string viewId, out UIViewBase prefab)
        {
            if (TryGetEntry(viewId, out UIViewCatalogEntry entry))
            {
                prefab = entry.Prefab;
                return prefab != null;
            }

            prefab = null;
            return false;
        }

        public bool TryGet<T>(out T prefab) where T : UIViewBase
        {
            if (TryGetEntry(typeof(T), out UIViewCatalogEntry entry) && entry.Prefab is T typedPrefab)
            {
                prefab = typedPrefab;
                return true;
            }

            prefab = null;
            return false;
        }

        public bool TryGetEntry(string viewId, out UIViewCatalogEntry entry)
        {
            BuildLookup();
            return entriesById.TryGetValue(viewId, out entry);
        }

        public bool TryGetEntry(Type viewType, out UIViewCatalogEntry entry)
        {
            BuildLookup();
            if (entriesByType.TryGetValue(viewType, out entry))
                return true;

            return entriesById.TryGetValue(viewType.Name, out entry);
        }

        public bool TryGetEntry<T>(out UIViewCatalogEntry entry) where T : UIViewBase =>
            TryGetEntry(typeof(T), out entry);

        private void BuildLookup()
        {
            if (entriesById != null)
                return;

            entriesById = new Dictionary<string, UIViewCatalogEntry>(StringComparer.Ordinal);
            entriesByType = new Dictionary<Type, UIViewCatalogEntry>();

            foreach (UIViewCatalogEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ViewId))
                    continue;

                RegisterEntry(entry);
            }
        }

        private void RegisterEntry(UIViewCatalogEntry entry)
        {
            entriesById[entry.ViewId] = entry;
            if (entry.ViewType != null)
                entriesByType[entry.ViewType] = entry;
        }

        private void OnEnable()
        {
            entriesById = null;
            entriesByType = null;
        }
    }
}
