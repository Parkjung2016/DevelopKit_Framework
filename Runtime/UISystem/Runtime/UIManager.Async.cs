#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public sealed partial class UIManager
    {
        /// <summary>Addressable 로드 포함, Screen UI를 비동기로 엽니다.</summary>
        public async UniTask<UIViewResult> OpenScreenAsync<T>(
            object context = null,
            CancellationToken cancellationToken = default) where T : UIViewBase
        {
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found in catalog: {typeof(T).Name}");

            UIViewBase instance = await GetOrCreateInstanceAsync(entry, cancellationToken);
            if (instance is not UIScreenBase screen)
                return UIViewResult.Failed($"OpenScreen requires UIScreenBase: {typeof(T).Name}");

            await screenStack.PushAsync(screen, context, cancellationToken);
            TrackFloating(screen);
            RefreshSorting();
            return UIViewResult.Succeeded(screen);
        }

        /// <summary>Addressable 로드 포함, 팝업을 비동기로 엽니다.</summary>
        public UniTask<UIViewResult> OpenPopupAsync<T>(
            object context = null,
            int? priority = null,
            CancellationToken cancellationToken = default) where T : UIViewBase =>
            OpenPopupAsyncInternal<T>(new UIViewOpenOptions(context, priority), cancellationToken);

        /// <summary>Addressable 로드 포함, 팝업을 옵션과 함께 비동기로 엽니다.</summary>
        public UniTask<UIViewResult> OpenPopupAsync<T>(
            UIViewOpenOptions options,
            CancellationToken cancellationToken = default) where T : UIViewBase =>
            OpenPopupAsyncInternal<T>(options, cancellationToken);

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
        public async UniTask CloseAsync(IUIView view, bool immediate = false, CancellationToken cancellationToken = default)
        {
            if (view == null)
                return;

            if (view is UIScreenBase screen)
            {
                if (view is UIViewBase screenView)
                    await screenView.HideAsync(immediate, cancellationToken);
                else
                    view.Hide(immediate);

                if (ReferenceEquals(screenStack.Peek, screen))
                    screenStack.PopSilently();
                else
                    screenStack.Remove(screen, hide: false);
            }
            else if (view is UIViewBase viewBase)
            {
                await viewBase.HideAsync(immediate, cancellationToken);
            }
            else
            {
                view.Hide(immediate);
            }

            UntrackFloating(view);
            RefreshSorting();
        }

        /// <summary>타입으로 열려 있는 팝업을 비동기로 닫습니다.</summary>
        public async UniTask ClosePopupAsync<T>(bool immediate = false, CancellationToken cancellationToken = default)
            where T : UIViewBase
        {
            if (viewCatalog != null && viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
            {
                if (instancesById.TryGetValue(entry.ViewId, out UIViewBase instance))
                {
                    await CloseAsync(instance, immediate, cancellationToken);
                    return;
                }
            }

            if (instancesById.TryGetValue(typeof(T).Name, out UIViewBase fallback))
                await CloseAsync(fallback, immediate, cancellationToken);
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
                int closed = screenStack.Count;
                if (closed == 0)
                    return 0;

                while (screenStack.Peek != null)
                {
                    UIScreenBase top = screenStack.Peek;
                    if (top is UIViewBase viewBase)
                        await viewBase.HideAsync(immediate, cancellationToken);
                    else
                        top.Hide(immediate);

                    screenStack.PopSilently();
                    UntrackFloating(top);
                }

                RefreshSorting();
                return closed;
            }

            CollectVisibleByLayer(layerId, closeBuffer);
            for (int i = closeBuffer.Count - 1; i >= 0; i--)
                await CloseAsync(closeBuffer[i], immediate, cancellationToken);

            int count = closeBuffer.Count;
            closeBuffer.Clear();
            return count;
        }

        /// <summary>지정 Canvas 묶음 ID의 열려 있는 UI를 비동기로 모두 닫습니다.</summary>
        public async UniTask<int> CloseCanvasGroupAsync(
            string groupId,
            bool immediate = false,
            CancellationToken cancellationToken = default)
        {
            UILayerUtility.GetLayerIdsInGroup(groupId, layerRegistry, layerIdBuffer);
            int closed = 0;
            for (int i = 0; i < layerIdBuffer.Count; i++)
                closed += await CloseLayerAsync(layerIdBuffer[i], immediate, cancellationToken);

            layerIdBuffer.Clear();
            return closed;
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
            CollectVisibleOfType(typeof(T), closeBuffer);
            for (int i = closeBuffer.Count - 1; i >= 0; i--)
                await CloseAsync(closeBuffer[i], immediate, cancellationToken);

            int closed = closeBuffer.Count;
            closeBuffer.Clear();
            return closed;
        }

        private async UniTask<UIViewResult> OpenPopupAsyncInternal<T>(
            UIViewOpenOptions options,
            CancellationToken cancellationToken) where T : UIViewBase
        {
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found in catalog: {typeof(T).Name}");

            return await OpenInstanceAsync(entry, options, cancellationToken);
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
            await instance.ShowAsync(options.Context, cancellationToken);
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
            return await UIViewInstanceFactory.GetOrCreateAsync(entry, parent, instancesById, cancellationToken);
        }
    }
}
#endif
