using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UIView 카탈로그 항목입니다. 프리팹 직접 참조 또는 Addressable 키를 지정합니다.</summary>
    [Serializable]
    public sealed class UIViewCatalogEntry
    {
        [SerializeField]
        private string viewId;

        [SerializeField]
        private UIViewBase prefab;

        [SerializeField]
        private string addressableKey;

        [SerializeField]
        private bool loadFromAddressable;

        internal UIViewCatalogEntry()
        {
        }

        public string ViewId => string.IsNullOrEmpty(viewId)
            ? prefab != null ? prefab.ViewId : string.Empty
            : viewId;

        public UIViewBase Prefab => prefab;

        public bool HasPrefab => prefab != null;

        public bool UseAddressable => loadFromAddressable || prefab == null;

        public string AddressableKey => string.IsNullOrEmpty(addressableKey) ? ViewId : addressableKey;

        public Type ViewType => prefab != null ? prefab.GetType() : null;
    }
}
