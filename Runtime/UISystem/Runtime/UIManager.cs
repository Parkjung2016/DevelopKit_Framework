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

        private readonly UILayerRegistry layerRegistry = new();
        private readonly UINavigationStack screenStack = new();
        private readonly List<IUIView> floatingViews = new();
        private readonly List<IUIView> showOrder = new();
        private readonly List<IUIView> closeBuffer = new();
        private readonly List<string> layerIdBuffer = new();
        private readonly Dictionary<string, UIViewBase> instancesById = new(StringComparer.Ordinal);

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
            viewCatalog = catalog;
            layerSettings = settings;
            layerRegistry.Initialize(settings ?? UILayerSettings.CreateBuiltIn());
            layerRegistryReady = true;
            EnsureLayerRoots();
        }

        /// <summary>화면 스택에 Screen UI를 엽니다.</summary>
        public UIViewResult OpenScreen<T>(object context = null) where T : UIViewBase
        {
            EnsureLayerRegistry();
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found in catalog: {typeof(T).Name}");

            UIViewBase instance = GetOrCreateInstance(entry);
            if (instance is not UIScreenBase screen)
                return UIViewResult.Failed($"OpenScreen requires UIScreenBase: {typeof(T).Name}");

            screenStack.Push(screen, context);
            TrackFloating(screen);
            RefreshSorting();
            return UIViewResult.Succeeded(screen);
        }

        /// <summary>화면 스택 최상단 Screen UI를 닫습니다.</summary>
        public bool CloseScreen()
        {
            UIScreenBase top = screenStack.Peek;
            if (top == null)
                return false;

            screenStack.TryPop();
            UntrackFloating(top);
            RefreshSorting();
            return true;
        }

        /// <summary>팝업·모달·오버레이를 엽니다.</summary>
        public UIViewResult OpenPopup<T>(object context = null, int? priority = null) where T : UIViewBase =>
            OpenPopupInternal<T>(new UIViewOpenOptions(context, priority));

        /// <summary>팝업·모달·오버레이를 옵션과 함께 엽니다.</summary>
        public UIViewResult OpenPopup<T>(UIViewOpenOptions options) where T : UIViewBase =>
            OpenPopupInternal<T>(options);

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
            if (view == null)
                return;

            if (view is UIScreenBase screen)
            {
                if (ReferenceEquals(screenStack.Peek, screen))
                    screenStack.TryPop();
                else
                {
                    view.Hide(immediate);
                    screenStack.Remove(screen, hide: false);
                }
            }
            else
            {
                view.Hide(immediate);
            }

            UntrackFloating(view);
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

        /// <summary>타입으로 열려 있는 UI를 닫습니다.</summary>
        public void ClosePopup<T>(bool immediate = false) where T : UIViewBase
        {
            if (viewCatalog != null && viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
            {
                if (instancesById.TryGetValue(entry.ViewId, out UIViewBase instance))
                {
                    Close(instance, immediate);
                    return;
                }
            }

            if (instancesById.TryGetValue(typeof(T).Name, out UIViewBase fallback))
                Close(fallback, immediate);
        }

        /// <summary>뷰 ID로 열려 있는 UI를 닫습니다.</summary>
        public void ClosePopup(string viewId, bool immediate = false)
        {
            if (instancesById.TryGetValue(viewId, out UIViewBase instance))
                Close(instance, immediate);
        }

        /// <summary>Back 입력을 처리합니다. 처리했으면 true를 반환합니다.</summary>
        public bool TryHandleBack()
        {
            if (UIBackDispatcher.TryHandleBack(screenStack, floatingViews, layerRegistry))
                return true;

            BackUnhandled?.Invoke();
            return false;
        }

        public void ClearAll(bool includeScreens = true)
        {
            for (int i = floatingViews.Count - 1; i >= 0; i--)
                floatingViews[i].Hide();

            floatingViews.Clear();
            showOrder.Clear();

            if (includeScreens)
                screenStack.Clear();

            RefreshCanvasRaycasters();
        }

        private int CloseScreenLayer(bool immediate)
        {
            int count = screenStack.Count;
            if (count == 0)
                return 0;

            screenStack.Clear(immediate);

            for (int i = floatingViews.Count - 1; i >= 0; i--)
            {
                if (layerRegistry.IsScreenLayer(floatingViews[i].LayerId))
                    UntrackFloating(floatingViews[i]);
            }

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

        private UIViewResult OpenPopupInternal<T>(UIViewOpenOptions options) where T : UIViewBase
        {
            EnsureLayerRegistry();
            EnsureLayerRoots();

            if (viewCatalog == null)
                return UIViewResult.Failed("UIViewCatalog is not assigned. Call Initialize first.");

            if (!viewCatalog.TryGetEntry<T>(out UIViewCatalogEntry entry))
                return UIViewResult.Failed($"View not found in catalog: {typeof(T).Name}");

            return OpenInstance(entry, options);
        }

        private UIViewResult OpenInstance(UIViewCatalogEntry entry, UIViewOpenOptions options)
        {
            UIViewBase instance = GetOrCreateInstance(entry);
            if (instance == null)
                return UIViewResult.Failed($"Failed to create view: {entry.ViewId}");

            ApplyOpenOptions(instance, options);
            instance.Show(options.Context);
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
            return UIViewInstanceFactory.GetOrCreateFromPrefab(entry, parent, instancesById);
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
            List<IUIView> visible = new();
            for (int i = 0; i < floatingViews.Count; i++)
            {
                if (floatingViews[i].IsVisible)
                    visible.Add(floatingViews[i]);
            }

            UIViewSortUtility.SortByBackPriority(visible, showOrder, layerRegistry);

            for (int i = 0; i < visible.Count; i++)
            {
                if (visible[i] is UIViewBase viewBase)
                    viewBase.SetRuntimeSorting(i);
            }

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
