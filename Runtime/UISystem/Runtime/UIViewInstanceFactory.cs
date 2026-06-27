using System.Threading;
using PJDev.DevelopKit.BasicTemplate.Runtime;
#if UNITASK_INSTALLED
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>Addressable 또는 카탈로그 프리팹으로 UIView 인스턴스를 생성합니다.</summary>
    internal static class UIViewInstanceFactory
    {
        public static UIViewBase GetOrCreateFromPrefab(
            UIViewCatalogEntry entry,
            RectTransform parent,
            System.Collections.Generic.Dictionary<string, UIViewBase> instancesById)
        {
            if (entry == null)
                return null;

            if (instancesById.TryGetValue(entry.ViewId, out UIViewBase existing))
                return existing;

            if (!entry.HasPrefab)
            {
                if (!entry.UseAddressable)
                    return null;

                GameObject cachedPrefab = AddressableManager.Instance.Load<GameObject>(entry.AddressableKey);
                if (cachedPrefab == null)
                    return null;

                UIViewBase loadedInstance = Object.Instantiate(cachedPrefab, parent).GetComponent<UIViewBase>();
                if (loadedInstance == null)
                    return null;

                loadedInstance.name = entry.ViewId;
                loadedInstance.Hide(immediate: true);
                instancesById[entry.ViewId] = loadedInstance;
                return loadedInstance;
            }

            UIViewBase instance = Object.Instantiate(entry.Prefab, parent);
            instance.name = entry.ViewId;
            instance.Hide(immediate: true);
            instancesById[entry.ViewId] = instance;
            return instance;
        }

#if UNITASK_INSTALLED
        public static async UniTask<UIViewBase> GetOrCreateAsync(
            UIViewCatalogEntry entry,
            RectTransform parent,
            System.Collections.Generic.Dictionary<string, UIViewBase> instancesById,
            CancellationToken cancellationToken = default)
        {
            if (entry == null)
                return null;

            if (instancesById.TryGetValue(entry.ViewId, out UIViewBase existing))
                return existing;

            cancellationToken.ThrowIfCancellationRequested();

            if (entry.UseAddressable)
            {
                GameObject instanceObject = await AddressableManager.Instance
                    .InstantiateGameObjectAsync(entry.AddressableKey, parent)
                    .AttachExternalCancellation(cancellationToken);

                if (instanceObject == null)
                    return null;

                UIViewBase instance = instanceObject.GetComponent<UIViewBase>();
                if (instance == null)
                {
                    CDebug.LogError($"Addressable UI '{entry.AddressableKey}' has no UIViewBase component.");
                    Object.Destroy(instanceObject);
                    return null;
                }

                instance.name = entry.ViewId;
                instance.Hide(immediate: true);
                instancesById[entry.ViewId] = instance;
                return instance;
            }

            return GetOrCreateFromPrefab(entry, parent, instancesById);
        }
#endif
    }
}
