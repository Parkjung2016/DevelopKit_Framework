using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UI 표시·스택·Back 라우팅을 담당하는 중앙 매니저입니다.</summary>
    public sealed partial class UIManager : Singleton<UIManager>
    {
        private UIViewCatalog viewCatalog;
        private UILayerSettings layerSettings;
        private UILayerRoots layerRoots;
        private GameObject uIRootObject;
        private bool layerRegistryReady;
        private int instanceGeneration;

        private readonly UILayerRegistry layerRegistry = new();
        private readonly UINavigationStack screenStack = new();
        private readonly List<IUIView> floatingViews = new();
        private readonly List<IUIView> showOrder = new();
        private readonly List<IUIView> closeBuffer = new();
        private readonly List<IUIView> visibleBuffer = new();
        private readonly List<IUIView> backCandidateBuffer = new();
        private readonly List<string> layerIdBuffer = new();
        private readonly List<string> staleInstanceKeys = new();
        private readonly List<UIScreenBase> screenBuffer = new();
        private readonly Dictionary<string, UIViewBase> instancesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId = new(StringComparer.Ordinal);

        /// <summary>레이어 설정 레지스트리입니다.</summary>
        public UILayerRegistry LayerRegistry
        {
            get
            {
                EnsureLayerRegistry();
                return layerRegistry;
            }
        }

        /// <summary>카탈로그가 설정되었는지 여부입니다.</summary>
        public bool IsInitialized => viewCatalog != null;

        /// <summary>Back 입력이 아무 뷰에서도 처리되지 않았을 때 호출됩니다.</summary>
        public event Action BackUnhandled;

        /// <summary>카탈로그와 (선택) 레이어 설정으로 UI 시스템을 초기화합니다. 캔버스 루트는 자동으로 찾거나 생성합니다.</summary>
        public void Initialize(UIViewCatalog catalog, UILayerSettings settings = null)
        {
            instanceGeneration++;
            viewCatalog = catalog;
            layerSettings = settings;
            layerRegistry.Initialize(settings ?? UILayerSettings.CreateBuiltIn());
            layerRegistryReady = true;
            PurgeDeadReferences();
            EnsureLayerRoots();
        }

        /// <summary>화면 스택에 Screen UI를 엽니다.</summary>
        public UIViewResult<T> OpenScreen<T>(object context = null) where T : UIViewBase
        {
            EnsureLayerRegistry();
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult<T>.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult<T>.Failed($"View not found in catalog: {typeof(T).Name}");

            return ToTypedResult<T>(OpenScreenEntry(entry, context));
        }

        /// <summary>뷰 ID로 화면 스택에 Screen UI를 엽니다.</summary>
        public UIViewResult<T> OpenScreen<T>(string viewId, object context = null) where T : UIViewBase =>
            ToTypedResult<T>(OpenScreen(viewId, context));

        /// <summary>뷰 ID로 화면 스택에 Screen UI를 엽니다.</summary>
        public UIViewResult OpenScreen(string viewId, object context = null)
        {
            EnsureLayerRegistry();
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry(viewId, out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found: {viewId}");

            return OpenScreenEntry(entry, context);
        }

        /// <summary>화면 스택 최상단 Screen UI를 닫습니다.</summary>
        public bool CloseScreen()
        {
            UIScreenBase top = screenStack.Peek;
            if (top == null)
                return false;

            if (IsClosePending(top))
                return false;

            screenStack.TryPop();
            UntrackFloating(top);
            TryReleaseInstance(top);
            RefreshSorting();
            return true;
        }

        /// <summary>팝업·모달·오버레이를 엽니다.</summary>
        public UIViewResult<T> OpenPopup<T>(object context = null, int? priority = null) where T : UIViewBase =>
            OpenPopupInternal<T>(new UIViewOpenOptions(context, priority));

        /// <summary>팝업·모달·오버레이를 옵션과 함께 엽니다.</summary>
        public UIViewResult<T> OpenPopup<T>(UIViewOpenOptions options) where T : UIViewBase =>
            OpenPopupInternal<T>(options);

        /// <summary>뷰 ID로 팝업·모달·오버레이를 엽니다.</summary>
        public UIViewResult<T> OpenPopup<T>(string viewId, object context = null, int? priority = null) where T : UIViewBase =>
            OpenPopup<T>(viewId, new UIViewOpenOptions(context, priority));

        /// <summary>뷰 ID로 팝업·모달·오버레이를 옵션과 함께 엽니다.</summary>
        public UIViewResult<T> OpenPopup<T>(string viewId, UIViewOpenOptions options) where T : UIViewBase
        {
            UIViewResult result = OpenPopup(viewId, options);
            return ToTypedResult<T>(result);
        }

        /// <summary>뷰 ID로 팝업·모달·오버레이를 엽니다.</summary>
        public UIViewResult OpenPopup(string viewId, object context = null, int? priority = null) =>
            OpenPopup(viewId, new UIViewOpenOptions(context, priority));

        /// <summary>뷰 ID로 팝업·모달·오버레이를 옵션과 함께 엽니다.</summary>
        public UIViewResult OpenPopup(string viewId, UIViewOpenOptions options)
        {
            EnsureLayerRegistry();
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry(viewId, out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found: {viewId}");

            return OpenInstance(entry, options);
        }

        /// <summary>열려 있는 UI를 닫습니다.</summary>
        public void Close(IUIView view, bool immediate = false)
        {
            if (!IsViewAlive(view))
            {
                PurgeDeadReferences();
                return;
            }

            if (IsClosePending(view))
                return;

            if (view is UIScreenBase screen)
            {
                if (ReferenceEquals(screenStack.Peek, screen))
                    screenStack.TryPop();
                else
                {
                    UIViewLifecycle.Hide(view, immediate);
                    screenStack.Remove(screen, hide: false);
                }
            }
            else
            {
                UIViewLifecycle.Hide(view, immediate);
            }

            UntrackFloating(view);
            TryReleaseInstance(view as UIViewBase);
            RefreshSorting();
        }

        /// <summary>지정 레이어 ID의 열려 있는 UI를 모두 닫습니다.</summary>
        public int CloseLayer(string layerId, bool immediate = false)
        {
            EnsureLayerRegistry();

            if (layerRegistry.IsScreenLayer(layerId))
                return CloseScreenLayer(immediate);

            CollectVisibleByLayer(layerId, closeBuffer);
            for (int i = closeBuffer.Count - 1; i >= 0; i--)
                Close(closeBuffer[i], immediate);

            int closed = closeBuffer.Count;
            closeBuffer.Clear();
            return closed;
        }

        /// <summary>지정 Canvas 묶음 ID의 열려 있는 UI를 모두 닫습니다.</summary>
        public int CloseCanvasGroup(string groupId, bool immediate = false)
        {
            EnsureLayerRegistry();
            UILayerUtility.GetLayerIdsInGroup(groupId, layerRegistry, layerIdBuffer);
            int closed = 0;
            for (int i = 0; i < layerIdBuffer.Count; i++)
                closed += CloseLayer(layerIdBuffer[i], immediate);

            layerIdBuffer.Clear();
            return closed;
        }

        /// <summary>지정 Canvas 묶음의 열려 있는 UI를 모두 닫습니다.</summary>
        public int CloseCanvasGroup(UICanvasGroup group, bool immediate = false) =>
            CloseCanvasGroup(UICanvasGroupUtility.EnumToId(group), immediate);

        /// <summary>T 또는 T 파생 타입의 열려 있는 UI를 모두 닫습니다.</summary>
        public int CloseAllOfType<T>(bool immediate = false) where T : UIViewBase
        {
            CollectVisibleOfType(typeof(T), closeBuffer);
            for (int i = closeBuffer.Count - 1; i >= 0; i--)
                Close(closeBuffer[i], immediate);

            int closed = closeBuffer.Count;
            closeBuffer.Clear();
            return closed;
        }

        /// <summary>뷰 ID로 열려 있는 UI를 모두 닫습니다. 중복 허용 팝업(토스트 등)에 사용합니다.</summary>
        public int CloseAllOfViewId(string viewId, bool immediate = false)
        {
            if (AllowsMultipleInstances(viewId))
                return CloseAllMatchingViewId(viewId, immediate);

            if (UIViewInstanceCache.TryGetAlive(instancesById, viewId, out UIViewBase instance))
            {
                Close(instance, immediate);
                return 1;
            }

            return CloseAllMatchingViewId(viewId, immediate);
        }

        /// <summary>타입으로 열려 있는 UI를 닫습니다. 중복 허용이면 해당 viewId 전부 닫습니다.</summary>
        public void ClosePopup<T>(bool immediate = false) where T : UIViewBase
        {
            if (viewCatalog != null && viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
            {
                if (entry.AllowMultipleInstances)
                {
                    CloseAllOfViewId(entry.ViewId, immediate);
                    return;
                }

                if (UIViewInstanceCache.TryGetAlive(instancesById, entry.ViewId, out UIViewBase instance))
                {
                    Close(instance, immediate);
                    return;
                }
            }

            if (UIViewInstanceCache.TryGetAlive(instancesById, typeof(T).Name, out UIViewBase fallback))
                Close(fallback, immediate);
        }

        /// <summary>뷰 ID로 열려 있는 UI를 닫습니다.</summary>
        public void ClosePopup(string viewId, bool immediate = false) =>
            CloseAllOfViewId(viewId, immediate);

        /// <summary>Back 입력을 처리합니다. 처리했으면 true를 반환합니다.</summary>
        public bool TryHandleBack()
        {
            bool handled = UIBackDispatcher.TryHandleBack(
                screenStack,
                floatingViews,
                layerRegistry,
                backCandidateBuffer);
            backCandidateBuffer.Clear();

            if (handled)
                return true;

            BackUnhandled?.Invoke();
            return false;
        }

        /// <summary>열려 있는 UI를 숨깁니다. 풀링된 인스턴스는 유지됩니다.</summary>
        public void ClearAll(bool includeScreens = true)
        {
            instanceGeneration++;
            ClearVisibleViews(includeScreens, releaseInstances: true);
        }

        /// <summary>열려 있는 UI를 닫고 선택한 범위의 인스턴스를 모두 파괴합니다.</summary>
        public void DisposeAll(bool includeScreens = true)
        {
            instanceGeneration++;
            CollectAllInstances(closeBuffer, includeScreens);
            ClearVisibleViews(includeScreens, releaseInstances: false);
            DestroyInstances(closeBuffer);

            if (includeScreens)
            {
                instancesById.Clear();
                UIViewInstanceCache.ClearDuplicates(duplicatePoolsByViewId);
                UIViewDuplicateInstanceNaming.ResetAll();
            }

            closeBuffer.Clear();
        }

        private void ClearVisibleViews(bool includeScreens, bool releaseInstances)
        {
            visibleBuffer.Clear();
            for (int i = 0; i < floatingViews.Count; i++)
            {
                IUIView view = floatingViews[i];
                if (view is not UIScreenBase && IsViewAlive(view))
                    visibleBuffer.Add(view);
            }

            for (int i = visibleBuffer.Count - 1; i >= 0; i--)
            {
                IUIView view = visibleBuffer[i];
                if (IsClosePending(view))
                    continue;

                UIViewLifecycle.Hide(view);
                UntrackFloating(view);
                if (releaseInstances)
                    TryReleaseInstance(view as UIViewBase);
            }

            visibleBuffer.Clear();

            if (includeScreens)
            {
                screenStack.DrainTo(screenBuffer);
                for (int i = 0; i < screenBuffer.Count; i++)
                {
                    UIScreenBase screen = screenBuffer[i];
                    if (IsClosePending(screen))
                        continue;

                    UntrackFloating(screen);
                    if (releaseInstances)
                        TryReleaseInstance(screen);
                }

                screenBuffer.Clear();
            }

            RefreshSorting();
        }

        private int CloseScreenLayer(bool immediate)
        {
            int count = screenStack.Count;
            if (count == 0)
                return 0;

            screenStack.DrainTo(screenBuffer, immediate);
            for (int i = 0; i < screenBuffer.Count; i++)
            {
                if (IsClosePending(screenBuffer[i]))
                    continue;

                UntrackFloating(screenBuffer[i]);
                TryReleaseInstance(screenBuffer[i]);
            }

            screenBuffer.Clear();
            RefreshSorting();
            return count;
        }

        private void CollectVisibleByLayer(string layerId, List<IUIView> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < floatingViews.Count; i++)
            {
                IUIView view = floatingViews[i];
                if (view.LayerId == layerId && view.IsVisible)
                    buffer.Add(view);
            }
        }

        private void CollectVisibleOfType(Type viewType, List<IUIView> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < floatingViews.Count; i++)
            {
                IUIView view = floatingViews[i];
                if (!view.IsVisible)
                    continue;

                if (view is UIViewBase viewBase && viewType.IsAssignableFrom(viewBase.GetType()))
                    buffer.Add(view);
            }
        }

        private UIViewResult<T> OpenPopupInternal<T>(UIViewOpenOptions options) where T : UIViewBase
        {
            EnsureLayerRegistry();
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult<T>.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult<T>.Failed($"View not found in catalog: {typeof(T).Name}");

            UIViewResult result = OpenInstance(entry, options);
            return ToTypedResult<T>(result);
        }

        private static UIViewResult<T> ToTypedResult<T>(UIViewResult result) where T : UIViewBase
        {
            if (!result.IsSuccess)
                return UIViewResult<T>.Failed(result.ErrorMessage);

            if (result.Handle.View is T typedView)
                return UIViewResult<T>.Succeeded(typedView);

            return UIViewResult<T>.Failed(CreateTypeMismatchMessage<T>(result.Handle.View as UIViewBase));
        }

        private static string CreateTypeMismatchMessage<T>(UIViewBase instance) where T : UIViewBase =>
            $"View type mismatch: expected {typeof(T).Name}, got {instance?.GetType().Name ?? "null"}";

        private UIViewResult OpenScreenEntry(UIViewCatalogEntry entry, object context)
        {
            UIViewBase instance = GetOrCreateInstance(entry);
            if (instance is not UIScreenBase screen)
                return UIViewResult.Failed($"OpenScreen requires UIScreenBase: {entry.ViewId}");

            screenStack.Push(screen, context);
            TrackFloating(screen);
            RefreshSorting();
            return UIViewResult.Succeeded(screen);
        }

        private UIViewResult OpenInstance(UIViewCatalogEntry entry, UIViewOpenOptions options)
        {
            UIViewBase instance = GetOrCreateInstance(entry);
            if (instance == null)
                return UIViewResult.Failed($"Failed to create view: {entry.ViewId}");

            ApplyOpenOptions(instance, options);
            UIViewLifecycle.Show(instance, options.Context);
            TrackFloating(instance);
            RefreshSorting();
            return UIViewResult.Succeeded(instance);
        }

        private void ApplyOpenOptions(UIViewBase instance, UIViewOpenOptions options)
        {
            string targetLayerId = string.IsNullOrEmpty(options.LayerId) ? instance.LayerId : options.LayerId;
            RectTransform parent = layerRoots.GetRoot(targetLayerId, layerRegistry);
            instance.ApplyOpenOverrides(options.LayerId, options.Priority, parent);
        }

        private UIViewBase GetOrCreateInstance(UIViewCatalogEntry entry)
        {
            if (entry == null)
                return null;

            EnsureLayerRegistry();
            EnsureLayerRoots();

            if (layerRoots == null)
                return null;

            string layerId = entry.Prefab != null ? entry.Prefab.LayerId : UILayers.Popup;
            RectTransform parent = layerRoots.GetRoot(layerId, layerRegistry);
            PurgeDeadReferences();
#if UNITASK_INSTALLED
            if (!entry.AllowMultipleInstances && pendingSingletonInstances.ContainsKey(entry.ViewId))
                return null;
#endif
            return UIViewInstanceFactory.GetOrCreateFromPrefab(
                entry, parent, instancesById, duplicatePoolsByViewId);
        }

        private void TrackFloating(IUIView view)
        {
            if (layerRegistry.IsScreenLayer(view.LayerId))
            {
                if (!floatingViews.Contains(view))
                    floatingViews.Add(view);
            }
            else if (!floatingViews.Contains(view))
            {
                floatingViews.Add(view);
            }

            showOrder.Remove(view);
            showOrder.Add(view);
        }

        private void UntrackFloating(IUIView view)
        {
            floatingViews.Remove(view);
            showOrder.Remove(view);
        }

        private void RefreshSorting()
        {
            PurgeDeadReferences();

            visibleBuffer.Clear();
            for (int i = 0; i < floatingViews.Count; i++)
            {
                if (floatingViews[i].IsVisible)
                    visibleBuffer.Add(floatingViews[i]);
            }

            UIViewSortUtility.SortByBackPriority(visibleBuffer, showOrder, layerRegistry);

            for (int i = 0; i < visibleBuffer.Count; i++)
            {
                if (visibleBuffer[i] is UIViewBase viewBase)
                    viewBase.SetRuntimeSorting(i);
            }

            visibleBuffer.Clear();
            RefreshCanvasRaycasters();
        }

        private void RefreshCanvasRaycasters()
        {
            if (layerRoots == null)
                return;

            IReadOnlyList<string> groupIds = layerRegistry.AllCanvasGroupIds;
            for (int i = 0; i < groupIds.Count; i++)
                RefreshCanvasRaycaster(groupIds[i]);
        }

        private void RefreshCanvasRaycaster(string groupId)
        {
            bool hasVisible = false;
            for (int i = 0; i < floatingViews.Count; i++)
            {
                IUIView view = floatingViews[i];
                if (view.IsVisible && UILayerUtility.IsInGroup(view.LayerId, groupId, layerRegistry))
                {
                    hasVisible = true;
                    break;
                }
            }

            layerRoots.SetRaycasterEnabled(groupId, hasVisible);
        }

        private void TryReleaseInstance(UIViewBase view)
        {
            if (view == null)
            {
                PurgeStaleInstances();
                return;
            }

            if (ShouldPool(view))
                return;

            ReleaseDuplicateDisplayIndex(view);

            string viewId = view.ViewId;
            if (instancesById.TryGetValue(viewId, out UIViewBase cached) && ReferenceEquals(cached, view))
                instancesById.Remove(viewId);

            UIViewInstanceCache.RemoveDuplicate(duplicatePoolsByViewId, viewId, view);

            UnityEngine.Object.Destroy(view.gameObject);
        }

        private bool ShouldPool(UIViewBase view)
        {
            if (view == null)
                return false;

            if (viewCatalog != null && viewCatalog.TryGetEntry(view.ViewId, out UIViewCatalogEntry entry))
                return entry.UsePooling;

            return false;
        }

        private void DestroyAllInstances()
        {
            CollectAllInstances(closeBuffer, includeScreens: true);
            DestroyInstances(closeBuffer);
            instancesById.Clear();
            UIViewInstanceCache.ClearDuplicates(duplicatePoolsByViewId);
            UIViewDuplicateInstanceNaming.ResetAll();
            closeBuffer.Clear();
        }

        private int CloseAllMatchingViewId(string viewId, bool immediate)
        {
            CollectByViewId(viewId, closeBuffer);
            for (int i = closeBuffer.Count - 1; i >= 0; i--)
                Close(closeBuffer[i], immediate);

            int closed = closeBuffer.Count;
            closeBuffer.Clear();
            return closed;
        }

        private void CollectByViewId(string viewId, List<IUIView> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < floatingViews.Count; i++)
            {
                if (floatingViews[i] is not UIViewBase viewBase || !IsViewAlive(viewBase))
                    continue;

                if (viewBase.ViewId == viewId)
                    buffer.Add(viewBase);
            }
        }

        private void CollectAllInstances(List<IUIView> buffer, bool includeScreens)
        {
            buffer.Clear();

            for (int i = 0; i < floatingViews.Count; i++)
            {
                if (IsViewAlive(floatingViews[i])
                    && (includeScreens || floatingViews[i] is not UIScreenBase))
                    buffer.Add(floatingViews[i]);
            }

            if (includeScreens)
            {
                screenStack.CopyTo(screenBuffer);
                for (int i = 0; i < screenBuffer.Count; i++)
                {
                    UIScreenBase screen = screenBuffer[i];
                    if (IsViewAlive(screen) && !buffer.Contains(screen))
                        buffer.Add(screen);
                }
            }

            foreach (UIViewBase instance in instancesById.Values)
            {
                if (IsViewAlive(instance)
                    && (includeScreens || instance is not UIScreenBase)
                    && !buffer.Contains(instance))
                    buffer.Add(instance);
            }

            foreach (KeyValuePair<string, List<UIViewBase>> pair in duplicatePoolsByViewId)
            {
                List<UIViewBase> pool = pair.Value;
                for (int i = 0; i < pool.Count; i++)
                {
                    UIViewBase instance = pool[i];
                    if (IsViewAlive(instance)
                        && (includeScreens || instance is not UIScreenBase)
                        && !buffer.Contains(instance))
                        buffer.Add(instance);
                }
            }

            screenBuffer.Clear();
        }

        private void DestroyInstances(List<IUIView> instances)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] is UIViewBase viewBase)
                    DestroyInstance(viewBase);
            }
        }

        private void DestroyInstance(UIViewBase view)
        {
            if (view == null)
                return;

            string viewId = view.ViewId;
            if (instancesById.TryGetValue(viewId, out UIViewBase cached)
                && ReferenceEquals(cached, view))
            {
                instancesById.Remove(viewId);
            }

            UIViewInstanceCache.RemoveDuplicate(duplicatePoolsByViewId, viewId, view);
            ReleaseDuplicateDisplayIndex(view);
            UnityEngine.Object.Destroy(view.gameObject);
        }

        private static void ReleaseDuplicateDisplayIndex(UIViewBase view)
        {
            if (view == null || view.DuplicateDisplayIndex <= 0)
                return;

            UIViewDuplicateInstanceNaming.Release(view.ViewId, view.DuplicateDisplayIndex);
        }

        private bool AllowsMultipleInstances(string viewId) =>
            viewCatalog != null
            && viewCatalog.TryGetEntry(viewId, out UIViewCatalogEntry entry)
            && entry.AllowMultipleInstances;

        private void PurgeStaleInstances()
        {
            UIViewInstanceCache.PurgeStale(instancesById, staleInstanceKeys);
            UIViewInstanceCache.PurgeStaleDuplicates(duplicatePoolsByViewId, staleInstanceKeys);
        }

        private void PurgeDeadReferences()
        {
            PurgeStaleInstances();

            for (int i = floatingViews.Count - 1; i >= 0; i--)
            {
                if (!IsViewAlive(floatingViews[i]))
                    floatingViews.RemoveAt(i);
            }

            for (int i = showOrder.Count - 1; i >= 0; i--)
            {
                if (!IsViewAlive(showOrder[i]))
                    showOrder.RemoveAt(i);
            }
        }

        private static bool IsViewAlive(IUIView view) =>
            view is UIViewBase viewBase && viewBase != null;

        private bool IsClosePending(IUIView view)
        {
#if UNITASK_INSTALLED
            return view != null && pendingCloses.ContainsKey(view);
#else
            return false;
#endif
        }

        private void RefreshCanvasRaycaster(UICanvasGroup group) =>
            RefreshCanvasRaycaster(UICanvasGroupUtility.EnumToId(group));

        private void EnsureLayerRegistry()
        {
            if (layerRegistryReady)
                return;

            layerRegistry.Initialize(layerSettings ?? UILayerSettings.CreateBuiltIn());
            layerRegistryReady = true;
        }

        private void EnsureLayerRoots()
        {
            EnsureLayerRegistry();

            if (layerRoots != null)
            {
                layerRoots.EnsureDefaults(layerRegistry);
                return;
            }

            layerRoots = UnityEngine.Object.FindAnyObjectByType<UILayerRoots>();
            if (layerRoots != null)
            {
                layerRoots.EnsureDefaults(layerRegistry);
                return;
            }

            if (uIRootObject != null)
                return;

            GameObject rootObject = new GameObject("UI Root", typeof(RectTransform), typeof(UILayerRoots));
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            layerRoots = rootObject.GetComponent<UILayerRoots>();
            layerRoots.EnsureDefaults(layerRegistry);
            uIRootObject = rootObject;
            UnityEngine.Object.DontDestroyOnLoad(rootObject);
        }
    }
}
