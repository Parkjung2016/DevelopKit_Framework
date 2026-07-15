#if UNITASK_INSTALLED
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using PJDev.DevelopKit.Framework.PoolSystem.Runtime;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public sealed partial class UIManager
    {
        private readonly Dictionary<string, UniTaskCompletionSource<UIViewBase>> pendingSingletonInstances =
            new(StringComparer.Ordinal);
        private readonly Dictionary<IUIView, UniTaskCompletionSource> pendingCloses = new();
        /// <summary>Addressable 로드 포함, Screen UI를 비동기로 엽니다.</summary>
        public async UniTask<UIViewResult<T>> OpenScreenAsync<T>(
            object context = null,
            CancellationToken cancellationToken = default) where T : UIViewBase
        {
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult<T>.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult<T>.Failed($"View not found in catalog: {typeof(T).Name}");

            UIViewResult result = await OpenScreenEntryAsync(entry, context, cancellationToken);
            if (!result.IsSuccess)
                return UIViewResult<T>.Failed(result.ErrorMessage);

            if (result.Handle.View is T typedScreen)
                return UIViewResult<T>.Succeeded(typedScreen);

            return UIViewResult<T>.Failed(
                $"View type mismatch: expected {typeof(T).Name}, got {result.Handle.View?.GetType().Name ?? "null"}");
        }

        /// <summary>뷰 ID로 Screen UI를 비동기로 엽니다.</summary>
        public async UniTask<UIViewResult<T>> OpenScreenAsync<T>(
            string viewId,
            object context = null,
            CancellationToken cancellationToken = default) where T : UIViewBase
        {
            UIViewResult result = await OpenScreenAsync(viewId, context, cancellationToken);
            if (!result.IsSuccess)
                return UIViewResult<T>.Failed(result.ErrorMessage);

            if (result.Handle.View is T typedScreen)
                return UIViewResult<T>.Succeeded(typedScreen);

            return UIViewResult<T>.Failed(
                $"View type mismatch: expected {typeof(T).Name}, got {result.Handle.View?.GetType().Name ?? "null"}");
        }

        /// <summary>뷰 ID로 Screen UI를 비동기로 엽니다.</summary>
        public async UniTask<UIViewResult> OpenScreenAsync(
            string viewId,
            object context = null,
            CancellationToken cancellationToken = default)
        {
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry(viewId, out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found: {viewId}");

            return await OpenScreenEntryAsync(entry, context, cancellationToken);
        }

        /// <summary>Addressable 로드 포함, 팝업을 비동기로 엽니다.</summary>
        public UniTask<UIViewResult<T>> OpenPopupAsync<T>(
            object context = null,
            int? priority = null,
            CancellationToken cancellationToken = default) where T : UIViewBase =>
            OpenPopupAsyncInternal<T>(new UIViewOpenOptions(context, priority), cancellationToken);

        /// <summary>Addressable 로드 포함, 팝업을 옵션과 함께 비동기로 엽니다.</summary>
        public UniTask<UIViewResult<T>> OpenPopupAsync<T>(
            UIViewOpenOptions options,
            CancellationToken cancellationToken = default) where T : UIViewBase =>
            OpenPopupAsyncInternal<T>(options, cancellationToken);

        /// <summary>뷰 ID로 팝업을 비동기로 엽니다.</summary>
        public UniTask<UIViewResult<T>> OpenPopupAsync<T>(
            string viewId,
            object context = null,
            int? priority = null,
            CancellationToken cancellationToken = default) where T : UIViewBase =>
            OpenPopupAsync<T>(viewId, new UIViewOpenOptions(context, priority), cancellationToken);

        /// <summary>뷰 ID로 팝업을 옵션과 함께 비동기로 엽니다.</summary>
        public async UniTask<UIViewResult<T>> OpenPopupAsync<T>(
            string viewId,
            UIViewOpenOptions options,
            CancellationToken cancellationToken = default) where T : UIViewBase
        {
            UIViewResult result = await OpenPopupAsync(viewId, options, cancellationToken);
            if (!result.IsSuccess)
                return UIViewResult<T>.Failed(result.ErrorMessage);

            if (result.Handle.View is T typedView)
                return UIViewResult<T>.Succeeded(typedView);

            return UIViewResult<T>.Failed(
                $"View type mismatch: expected {typeof(T).Name}, got {result.Handle.View?.GetType().Name ?? "null"}");
        }

        /// <summary>뷰 ID로 팝업을 비동기로 엽니다.</summary>
        public UniTask<UIViewResult> OpenPopupAsync(
            string viewId,
            object context = null,
            int? priority = null,
            CancellationToken cancellationToken = default) =>
            OpenPopupAsync(viewId, new UIViewOpenOptions(context, priority), cancellationToken);

        /// <summary>뷰 ID로 팝업을 옵션과 함께 비동기로 엽니다.</summary>
        public async UniTask<UIViewResult> OpenPopupAsync(
            string viewId,
            UIViewOpenOptions options,
            CancellationToken cancellationToken = default)
        {
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry(viewId, out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found: {viewId}");

            return await OpenInstanceAsync(entry, options, cancellationToken);
        }

        /// <summary>열려 있는 UI를 비동기로 닫습니다.</summary>
        public async UniTask CloseAsync(
            IUIView view,
            bool immediate = false,
            CancellationToken cancellationToken = default)
        {
            if (!IsViewAlive(view))
            {
                PurgeDeadReferences();
                return;
            }

            if (!pendingCloses.TryGetValue(view, out UniTaskCompletionSource completion))
            {
                completion = new UniTaskCompletionSource();
                pendingCloses.Add(view, completion);
                CloseViewInternalAsync(view, immediate, completion).Forget();
            }

            await completion.Task.AttachExternalCancellation(cancellationToken);
        }

        private async UniTaskVoid CloseViewInternalAsync(
            IUIView view,
            bool immediate,
            UniTaskCompletionSource completion)
        {
            try
            {
                await view.Hide(immediate, CancellationToken.None);

                if (view is UIScreenBase screen)
                {
                    if (ReferenceEquals(screenStack.Peek, screen))
                        screenStack.PopSilently();
                    else
                        screenStack.Remove(screen, hide: false);
                }

                UntrackFloating(view);
                TryReleaseInstance(view as UIViewBase);
                RefreshSorting();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                if (pendingCloses.TryGetValue(view, out UniTaskCompletionSource current)
                    && ReferenceEquals(current, completion))
                {
                    pendingCloses.Remove(view);
                }
            }
        }

        /// <summary>타입으로 열려 있는 팝업을 비동기로 닫습니다.</summary>
        public async UniTask ClosePopupAsync<T>(bool immediate = false, CancellationToken cancellationToken = default)
            where T : UIViewBase
        {
            if (viewCatalog != null && viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
            {
                if (entry.AllowMultipleInstances)
                {
                    await CloseAllOfViewIdAsync(entry.ViewId, immediate, cancellationToken);
                    return;
                }

                if (UIViewInstanceCache.TryGetAlive(instancesById, entry.ViewId, out UIViewBase instance))
                {
                    await CloseAsync(instance, immediate, cancellationToken);
                    return;
                }
            }

            if (UIViewInstanceCache.TryGetAlive(instancesById, typeof(T).Name, out UIViewBase fallback))
                await CloseAsync(fallback, immediate, cancellationToken);
        }

        /// <summary>뷰 ID로 열려 있는 UI를 모두 비동기로 닫습니다.</summary>
        public async UniTask<int> CloseAllOfViewIdAsync(
            string viewId,
            bool immediate = false,
            CancellationToken cancellationToken = default)
        {
            List<IUIView> views = ListPool<IUIView>.Rent();
            try
            {
                CollectByViewId(viewId, views);
                int count = views.Count;
                for (int i = count - 1; i >= 0; i--)
                    await CloseAsync(views[i], immediate, cancellationToken);

                return count;
            }
            finally
            {
                ListPool<IUIView>.Return(views);
            }
        }

        /// <summary>지정 레이어 ID의 열려 있는 UI를 비동기로 모두 닫습니다.</summary>
        public async UniTask<int> CloseLayerAsync(
            string layerId,
            bool immediate = false,
            CancellationToken cancellationToken = default)
        {
            EnsureLayerRegistry();

            if (layerRegistry.IsScreenLayer(layerId))
            {
                int count = screenStack.Count;
                while (screenStack.Peek != null)
                    await CloseAsync(screenStack.Peek, immediate, cancellationToken);

                return count;
            }

            List<IUIView> views = ListPool<IUIView>.Rent();
            try
            {
                CollectVisibleByLayer(layerId, views);
                int count = views.Count;
                for (int i = count - 1; i >= 0; i--)
                    await CloseAsync(views[i], immediate, cancellationToken);

                return count;
            }
            finally
            {
                ListPool<IUIView>.Return(views);
            }
        }

        /// <summary>지정 Canvas 묶음 ID의 열려 있는 UI를 비동기로 모두 닫습니다.</summary>
        public async UniTask<int> CloseCanvasGroupAsync(
            string groupId,
            bool immediate = false,
            CancellationToken cancellationToken = default)
        {
            List<string> layerIds = ListPool<string>.Rent();
            try
            {
                UILayerUtility.GetLayerIdsInGroup(groupId, layerRegistry, layerIds);
                int closed = 0;
                for (int i = 0; i < layerIds.Count; i++)
                    closed += await CloseLayerAsync(layerIds[i], immediate, cancellationToken);

                return closed;
            }
            finally
            {
                ListPool<string>.Return(layerIds);
            }
        }

        /// <summary>지정 Canvas 묶음의 열려 있는 UI를 비동기로 모두 닫습니다.</summary>
        public UniTask<int> CloseCanvasGroupAsync(
            UICanvasGroup group,
            bool immediate = false,
            CancellationToken cancellationToken = default) =>
            CloseCanvasGroupAsync(UICanvasGroupUtility.EnumToId(group), immediate, cancellationToken);

        /// <summary>T 또는 T 파생 타입의 열려 있는 UI를 비동기로 모두 닫습니다.</summary>
        public async UniTask<int> CloseAllOfTypeAsync<T>(
            bool immediate = false,
            CancellationToken cancellationToken = default) where T : UIViewBase
        {
            List<IUIView> views = ListPool<IUIView>.Rent();
            try
            {
                CollectVisibleOfType(typeof(T), views);
                int count = views.Count;
                for (int i = count - 1; i >= 0; i--)
                    await CloseAsync(views[i], immediate, cancellationToken);

                return count;
            }
            finally
            {
                ListPool<IUIView>.Return(views);
            }
        }

        private async UniTask<UIViewResult<T>> OpenPopupAsyncInternal<T>(
            UIViewOpenOptions options,
            CancellationToken cancellationToken) where T : UIViewBase
        {
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult<T>.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult<T>.Failed($"View not found in catalog: {typeof(T).Name}");

            UIViewResult result = await OpenInstanceAsync(entry, options, cancellationToken);
            if (!result.IsSuccess)
                return UIViewResult<T>.Failed(result.ErrorMessage);

            if (result.Handle.View is T typedView)
                return UIViewResult<T>.Succeeded(typedView);

            return UIViewResult<T>.Failed(
                $"View type mismatch: expected {typeof(T).Name}, got {result.Handle.View?.GetType().Name ?? "null"}");
        }

        private async UniTask<UIViewResult> OpenScreenEntryAsync(
            UIViewCatalogEntry entry,
            object context,
            CancellationToken cancellationToken)
        {
            UIViewBase instance = await GetOrCreateInstanceAsync(entry, cancellationToken);
            if (instance is not UIScreenBase screen)
                return UIViewResult.Failed($"OpenScreen requires UIScreenBase: {entry.ViewId}");

            await screenStack.PushAsync(screen, context, cancellationToken);
            TrackFloating(screen);
            RefreshSorting();
            return UIViewResult.Succeeded(screen);
        }

        private async UniTask<UIViewResult> OpenInstanceAsync(
            UIViewCatalogEntry entry,
            UIViewOpenOptions options,
            CancellationToken cancellationToken)
        {
            UIViewBase instance = await GetOrCreateInstanceAsync(entry, cancellationToken);
            if (instance == null)
                return UIViewResult.Failed($"Failed to create view: {entry.ViewId}");

            ApplyOpenOptions(instance, options);
            await instance.Show(options.Context, cancellationToken);
            TrackFloating(instance);
            RefreshSorting();
            return UIViewResult.Succeeded(instance);
        }

        private async UniTask<UIViewBase> GetOrCreateInstanceAsync(
            UIViewCatalogEntry entry,
            CancellationToken cancellationToken)
        {
            if (entry == null)
                return null;

            EnsureLayerRoots();
            if (layerRoots == null)
                return null;

            string layerId = entry.Prefab != null ? entry.Prefab.LayerId : UILayers.Popup;
            RectTransform parent = layerRoots.GetRoot(layerId, layerRegistry);
            PurgeDeadReferences();

            if (entry.AllowMultipleInstances)
            {
                return await UIViewInstanceFactory.GetOrCreateAsync(
                    entry,
                    parent,
                    instancesById,
                    duplicatePoolsByViewId,
                    cancellationToken);
            }

            if (UIViewInstanceCache.TryGetAlive(instancesById, entry.ViewId, out UIViewBase cached))
                return cached;

            cancellationToken.ThrowIfCancellationRequested();
            if (!pendingSingletonInstances.TryGetValue(
                    entry.ViewId,
                    out UniTaskCompletionSource<UIViewBase> completion))
            {
                completion = new UniTaskCompletionSource<UIViewBase>();
                pendingSingletonInstances.Add(entry.ViewId, completion);
                CreateSingletonInstanceAsync(
                    entry,
                    parent,
                    instanceGeneration,
                    completion).Forget();
            }

            return await completion.Task.AttachExternalCancellation(cancellationToken);
        }

        private async UniTaskVoid CreateSingletonInstanceAsync(
            UIViewCatalogEntry entry,
            RectTransform parent,
            int generation,
            UniTaskCompletionSource<UIViewBase> completion)
        {
            try
            {
                UIViewBase instance = await UIViewInstanceFactory.GetOrCreateAsync(
                    entry,
                    parent,
                    instancesById,
                    duplicatePoolsByViewId,
                    CancellationToken.None);

                if (this == null || generation != instanceGeneration)
                {
                    DestroyInstance(instance);
                    instance = null;
                }

                completion.TrySetResult(instance);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                if (pendingSingletonInstances.TryGetValue(entry.ViewId, out var current)
                    && ReferenceEquals(current, completion))
                {
                    pendingSingletonInstances.Remove(entry.ViewId);
                }
            }
        }
    }
}
#endif