using UnityEngine;
#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>모든 런타임 UI 뷰의 기본 MonoBehaviour입니다.</summary>
    public abstract partial class UIViewBase : MonoBehaviour, IUIView
    {
        [UILayerId] [SerializeField] private string layerId = string.Empty;

        [SerializeField] private int priority = 0;

        [SerializeField] private UIViewBackBehavior backBehavior = UIViewBackBehavior.CloseOnBack;

        private CanvasGroup canvasGroup;
        private UIViewState state = UIViewState.Hidden;
        private object currentContext;
        private string runtimeLayerId;
        private int? runtimePriority;
        private string catalogViewId;
        private int duplicateDisplayIndex;
        private int transitionVersion;

        /// <summary>뷰 ID입니다. 카탈로그 등록 시 viewId, 아니면 프리팹 루트 이름입니다.</summary>
        public virtual string ViewId => string.IsNullOrEmpty(catalogViewId) ? gameObject.name : catalogViewId;

        /// <summary>레이어 ID입니다. 비어 있으면 <see cref="DefaultLayerId"/>를 사용합니다.</summary>
        public string LayerId => string.IsNullOrEmpty(runtimeLayerId)
            ? (string.IsNullOrEmpty(layerId) ? DefaultLayerId : layerId)
            : runtimeLayerId;

        /// <summary>layerId가 비어 있을 때 쓰는 기본 레이어 ID입니다.</summary>
        public string DefaultLayerId => ResolveDefaultLayerId();

        public int Priority => runtimePriority ?? priority;

        public UIViewState State => state;

        public bool IsVisible => state is UIViewState.Showing or UIViewState.Shown;

        public bool CloseOnBack => backBehavior == UIViewBackBehavior.CloseOnBack;

        public bool BlocksBack => backBehavior != UIViewBackBehavior.PassThrough;

        public UIViewBackBehavior BackBehavior => backBehavior;

        protected object CurrentContext => currentContext;

        protected virtual string ResolveDefaultLayerId() => UILayers.Popup;

        protected virtual void Reset()
        {
            EnsureCanvasGroup();
        }

        protected virtual void Awake()
        {
            EnsureCanvasGroup();
            SetVisible(false, immediate: true);
        }

#if UNITASK_INSTALLED
        public async UniTask Show(object context = null, CancellationToken cancellationToken = default)
        {
            currentContext = context;
            if (IsVisible)
            {
                OnUpdate(context);
                return;
            }

            int version = ++transitionVersion;
            state = UIViewState.Showing;
            SetActiveHidden();
            try
            {
                await OnOpen(context, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (this == null || version != transitionVersion)
                    return;

                SetVisible(true);
                state = UIViewState.Shown;
            }
            catch
            {
                if (this != null && version == transitionVersion)
                {
                    SetVisible(false, immediate: true);
                    state = UIViewState.Hidden;
                    currentContext = null;
                    ClearOpenOverrides();
                }

                throw;
            }
        }
#else
        public void Show(object context = null)
        {
            currentContext = context;
            if (IsVisible)
            {
                OnUpdate(context);
                return;
            }

            state = UIViewState.Showing;
            SetActiveHidden();
            OnOpen(context);
            SetVisible(true);
            state = UIViewState.Shown;
        }
#endif

#if UNITASK_INSTALLED
        public async UniTask Hide(bool immediate = false, CancellationToken cancellationToken = default)
        {
            if (state is UIViewState.Hidden or UIViewState.Hiding)
                return;

            int version = ++transitionVersion;
            state = UIViewState.Hiding;
            try
            {
                await OnClose(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (this == null || version != transitionVersion)
                    return;

                SetVisible(false, immediate);
                state = UIViewState.Hidden;
                currentContext = null;
                ClearOpenOverrides();
            }
            catch
            {
                if (this != null && version == transitionVersion)
                {
                    SetVisible(true);
                    state = UIViewState.Shown;
                }

                throw;
            }
        }
#else
        public void Hide(bool immediate = false)
        {
            if (state is UIViewState.Hidden or UIViewState.Hiding)
                return;

            state = UIViewState.Hiding;
            OnClose();
            SetVisible(false, immediate);
            state = UIViewState.Hidden;
            currentContext = null;
            ClearOpenOverrides();
        }
#endif

        public bool HandleBack()
        {
            if (!IsVisible)
                return false;

            if (OnBack())
                return true;

            if (!CloseOnBack)
                return false;

            UIManager.Instance.Close(this);
            return true;
        }

        public void Close(bool immediate = false) => UIManager.Instance.Close(this, immediate);

#if UNITASK_INSTALLED
        protected virtual UniTask OnOpen(object context, CancellationToken cancellationToken = default) =>
            UniTask.CompletedTask;
#else
        protected virtual void OnOpen(object context)
        {
        }
#endif

        /// <summary>이미 열려 있을 때 같은 UI를 다시 열면 호출됩니다.</summary>
        protected virtual void OnUpdate(object context)
        {
        }

#if UNITASK_INSTALLED
        protected virtual UniTask OnClose(CancellationToken cancellationToken = default) =>
            UniTask.CompletedTask;
#else
        protected virtual void OnClose()
        {
        }
#endif

        /// <summary>셋팅이 끝나고 화면에 보이기 직전에 호출됩니다. 딤 배경 등에 사용합니다.</summary>
        protected virtual void OnBeforeVisible()
        {
        }

        /// <summary>화면에서 숨기기 직전에 호출됩니다.</summary>
        protected virtual void OnBeforeHidden()
        {
        }

        /// <summary>Back 입력 처리. true면 기본 닫기를 하지 않습니다.</summary>
        protected virtual bool OnBack() => false;

        internal void SetRuntimeSorting(int siblingIndex)
        {
            if (transform.parent != null)
                transform.SetSiblingIndex(siblingIndex);
        }

        internal void ApplyOpenOverrides(string layerOverride, int? priorityOverride, RectTransform parent = null)
        {
            if (!string.IsNullOrEmpty(layerOverride))
            {
                runtimeLayerId = layerOverride;
                if (parent != null)
                {
                    transform.SetParent(parent, false);
                    StretchRectToParent(transform as RectTransform);
                }
            }

            if (priorityOverride.HasValue)
                runtimePriority = priorityOverride.Value;
        }

        internal void SetCatalogViewId(string viewId) => catalogViewId = viewId;

        internal void SetDuplicateDisplayIndex(int index) => duplicateDisplayIndex = index;

        internal int DuplicateDisplayIndex => duplicateDisplayIndex;

        internal void ClearOpenOverrides()
        {
            runtimeLayerId = null;
            runtimePriority = null;
        }

        internal void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        internal static void StretchRectToParent(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetActiveHidden()
        {
            gameObject.SetActive(true);
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void SetVisible(bool visible, bool immediate = false)
        {
            if (visible)
            {
                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);

                OnBeforeVisible();
                EnsureCanvasGroup();
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                return;
            }

            OnBeforeHidden();
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }
}