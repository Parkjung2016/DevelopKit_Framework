using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UI 프리팹을 ID와 타입으로 찾을 수 있는 카탈로그입니다.</summary>
    [CreateAssetMenu(fileName = "UIViewCatalog", menuName = "PJDev/UISystem/View Catalog")]
    public sealed class UIViewCatalog : ScriptableObject
    {
        [SerializeField] private List<UIViewCatalogEntry> entries = new();

        private Dictionary<string, UIViewCatalogEntry> entriesById;
        private Dictionary<Type, UIViewCatalogEntry> entriesByType;

        /// <summary>카탈로그에 등록된 항목 개수입니다.</summary>
        public int Count => entries?.Count ?? 0;

        internal IReadOnlyList<UIViewCatalogEntry> GetEntriesForEditor() => entries;

        /// <summary>ID로 UI 프리팹을 찾습니다.</summary>
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

        /// <summary>View 타입으로 UI 프리팹을 찾습니다.</summary>
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

        /// <summary>ID에 해당하는 카탈로그 항목을 찾습니다.</summary>
        public bool TryGetEntry(string viewId, out UIViewCatalogEntry entry)
        {
            if (string.IsNullOrEmpty(viewId))
            {
                entry = null;
                return false;
            }

            BuildLookup();
            return entriesById.TryGetValue(viewId, out entry);
        }

        /// <summary>View 타입에 해당하는 카탈로그 항목을 찾습니다.</summary>
        public bool TryGetEntry(Type viewType, out UIViewCatalogEntry entry)
        {
            if (viewType == null)
            {
                entry = null;
                return false;
            }

            BuildLookup();
            if (entriesByType.TryGetValue(viewType, out entry))
                return true;

            return entriesById.TryGetValue(viewType.Name, out entry);
        }

        /// <summary>View 타입에 해당하는 카탈로그 항목을 찾습니다.</summary>
        public bool TryGetEntry<T>(out UIViewCatalogEntry entry) where T : UIViewBase =>
            TryGetEntry(typeof(T), out entry);

        private void BuildLookup()
        {
            if (entriesById != null)
                return;

            // 첫 조회 때만 테이블을 만들고 이후 호출에서는 Dictionary 조회만 수행합니다.
            entriesById = new Dictionary<string, UIViewCatalogEntry>(entries.Count, StringComparer.Ordinal);
            entriesByType = new Dictionary<Type, UIViewCatalogEntry>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                UIViewCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.ViewId))
                    continue;

                entriesById[entry.ViewId] = entry;
                if (entry.ViewType != null)
                    entriesByType[entry.ViewType] = entry;
            }
        }

        private void OnEnable() => InvalidateLookup();

#if UNITY_EDITOR
        private void OnValidate() => InvalidateLookup();
#endif

        private void InvalidateLookup()
        {
            entriesById = null;
            entriesByType = null;
        }
    }
}
