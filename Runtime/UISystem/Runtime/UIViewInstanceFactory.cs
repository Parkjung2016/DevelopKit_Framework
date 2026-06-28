using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
#if UNITASK_INSTALLED
using System.Threading;
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
            Dictionary<string, UIViewBase> instancesById,
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId)
        {
            if (entry == null)
                return null;

            if (entry.AllowMultipleInstances)
            {
                if (entry.UsePooling
                    && UIViewInstanceCache.TryGetIdleDuplicate(duplicatePoolsByViewId, entry.ViewId, out UIViewBase idle))
                    return idle;

                return CreateInstance(entry, parent, instancesById, duplicatePoolsByViewId, trackSingleton: false);
            }

            if (UIViewInstanceCache.TryGetAlive(instancesById, entry.ViewId, out UIViewBase existing))
                return existing;

            return CreateInstance(entry, parent, instancesById, duplicatePoolsByViewId, trackSingleton: true);
        }

#if UNITASK_INSTALLED
        public static async UniTask<UIViewBase> GetOrCreateAsync(
            UIViewCatalogEntry entry,
            RectTransform parent,
            Dictionary<string, UIViewBase> instancesById,
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            CancellationToken cancellationToken = default)
        {
            if (entry == null)
                return null;

            if (entry.AllowMultipleInstances)
            {
                if (entry.UsePooling
                    && UIViewInstanceCache.TryGetIdleDuplicate(duplicatePoolsByViewId, entry.ViewId, out UIViewBase idle))
                    return idle;

                cancellationToken.ThrowIfCancellationRequested();
                return await CreateInstanceAsync(entry, parent, instancesById, duplicatePoolsByViewId, trackSingleton: false, cancellationToken);
            }

            if (UIViewInstanceCache.TryGetAlive(instancesById, entry.ViewId, out UIViewBase existing))
                return existing;

            cancellationToken.ThrowIfCancellationRequested();
            return await CreateInstanceAsync(entry, parent, instancesById, duplicatePoolsByViewId, trackSingleton: true, cancellationToken);
        }
#endif

        private static UIViewBase CreateInstance(
            UIViewCatalogEntry entry,
            RectTransform parent,
            Dictionary<string, UIViewBase> instancesById,
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            bool trackSingleton)
        {
            if (entry.UseAddressable)
            {
                GameObject instanceObject = AddressableManager.Instance.Instantiate(entry.AddressableKey, parent);
                return RegisterAddressableInstance(
                    instanceObject, entry, instancesById, duplicatePoolsByViewId, trackSingleton);
            }

            if (!entry.HasPrefab)
                return null;

            UIViewBase instance = Object.Instantiate(entry.Prefab, parent);
            return RegisterInstance(instance, entry, instancesById, duplicatePoolsByViewId, trackSingleton);
        }

#if UNITASK_INSTALLED
        private static async UniTask<UIViewBase> CreateInstanceAsync(
            UIViewCatalogEntry entry,
            RectTransform parent,
            Dictionary<string, UIViewBase> instancesById,
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            bool trackSingleton,
            CancellationToken cancellationToken)
        {
            if (entry.UseAddressable)
            {
                GameObject instanceObject = await AddressableManager.Instance
                    .InstantiateAsync(entry.AddressableKey, parent, cancellationToken);

                return RegisterAddressableInstance(
                    instanceObject, entry, instancesById, duplicatePoolsByViewId, trackSingleton);
            }

            if (!entry.HasPrefab)
                return null;

            UIViewBase instance = Object.Instantiate(entry.Prefab, parent);
            return RegisterInstance(instance, entry, instancesById, duplicatePoolsByViewId, trackSingleton);
        }
#endif

        private static UIViewBase RegisterAddressableInstance(
            GameObject instanceObject,
            UIViewCatalogEntry entry,
            Dictionary<string, UIViewBase> instancesById,
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            bool trackSingleton)
        {
            if (instanceObject == null)
                return null;

            UIViewBase instance = instanceObject.GetComponent<UIViewBase>();
            if (instance == null)
            {
                CDebug.LogError($"Addressable UI '{entry.AddressableKey}' has no UIViewBase component.");
                Object.Destroy(instanceObject);
                return null;
            }

            return RegisterInstance(instance, entry, instancesById, duplicatePoolsByViewId, trackSingleton);
        }

        private static UIViewBase RegisterInstance(
            UIViewBase instance,
            UIViewCatalogEntry entry,
            Dictionary<string, UIViewBase> instancesById,
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            bool trackSingleton)
        {
            if (instance == null)
                return null;

            instance.SetCatalogViewId(entry.ViewId);

            if (trackSingleton)
            {
                instance.SetDuplicateDisplayIndex(0);
                instance.name = entry.ViewId;
                instancesById[entry.ViewId] = instance;
            }
            else
            {
                int displayIndex = UIViewDuplicateInstanceNaming.Allocate(entry.ViewId);
                instance.SetDuplicateDisplayIndex(displayIndex);
                instance.name = $"{entry.ViewId}_{displayIndex}";

                if (entry.UsePooling)
                    UIViewInstanceCache.RegisterDuplicate(duplicatePoolsByViewId, entry.ViewId, instance);
            }

            UIViewLifecycle.Hide(instance, immediate: true);
            return instance;
        }
    }
}
