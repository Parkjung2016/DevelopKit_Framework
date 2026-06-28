using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UIView 카탈로그 항목입니다. 프리팹 직접 참조 또는 Addressable(viewId 주소)로 등록합니다.</summary>
    [Serializable]
    public sealed class UIViewCatalogEntry
    {
        [SerializeField]
        private string viewId;

        [SerializeField]
        private UIViewBase prefab;

        [SerializeField]
        private bool loadFromAddressable;

        [SerializeField]
        private bool usePooling = true;

        [SerializeField]
        private bool allowMultipleInstances;

        internal UIViewCatalogEntry()
        {
        }

        public string ViewId => string.IsNullOrEmpty(viewId)
            ? prefab != null ? prefab.ViewId : string.Empty
            : viewId;

        public UIViewBase Prefab => prefab;

        public bool HasPrefab => prefab != null;

        public bool UseAddressable => loadFromAddressable || prefab == null;

        /// <summary>닫을 때 인스턴스를 유지(풀링)할지 여부입니다.</summary>
        public bool UsePooling => usePooling;

        /// <summary>같은 viewId UI를 동시에 여러 개 열 수 있습니다. (토스트 등) Screen은 항상 false입니다.</summary>
        public bool AllowMultipleInstances =>
            allowMultipleInstances && prefab is not UIScreenBase;

        /// <summary>Addressable 주소입니다. 항상 <see cref="ViewId"/>와 같습니다.</summary>
        public string AddressableKey => ViewId;

        public Type ViewType => prefab != null ? prefab.GetType() : null;
    }
}
