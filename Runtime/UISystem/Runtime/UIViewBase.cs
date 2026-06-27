using UnityEngine;
#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>모든 런타임 UI 뷰의 기본 MonoBehaviour입니다.</summary>
    public abstract class UIViewBase : MonoBehaviour, IUIView
    {
        [UILayerId]
        [SerializeField]
        private string layerId;

        [SerializeField]
        private int priority;

        [SerializeField]
        private UIViewBackBehavior backBehavior = UIViewBackBehavior.CloseOnBack;

        private CanvasGroup canvasGroup;
        private UIViewState state = UIViewState.Hidden;
        private object currentContext;
        private string runtimeLayerId;
        private int? runtimePriority;

        /// <summary>뷰 ID입니다. 프리팹 루트 오브젝트 이름과 같습니다.</summary>
        public virtual string ViewId => gameObject.name;

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

#if UNITASK_INSTALLED
        public async UniTask ShowAsync(object context = null, CancellationToken cancellationToken = default)
        {
            currentContext = context;
            if (IsVisible)
            {
                OnUpdate(context);
                return;
            }

            state = UIViewState.Showing;
            SetActiveHidden();
            await OnOpenAsync(context, cancellationToken);
            SetVisible(true);
            state = UIViewState.Shown;
        }
#endif

        public void Hide(bool immediate = false)
        {
            if (!IsVisible && state != UIViewState.Hidden)
                return;

            state = UIViewState.Hiding;
            OnClose();
            SetVisible(false, immediate);
            state = UIViewState.Hidden;
            currentContext = null;
            ClearOpenOverrides();
        }

#if UNITASK_INSTALLED
        public async UniTask HideAsync(bool immediate = false, CancellationToken cancellationToken = default)
        {
            if (!IsVisible && state != UIViewState.Hidden)
                return;

            state = UIViewState.Hiding;
            await OnCloseAsync(cancellationToken);
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

        /// <summary>열 때 호출됩니다. 아직 화면에 보이지 않은 상태에서 UI를 셋팅하세요.</summary>
        protected virtual void OnOpen(object context)
        {
        }

#if UNITASK_INSTALLED
        /// <summary>비동기로 열 때 호출됩니다. 아직 화면에 보이지 않은 상태에서 UI를 셋팅하세요.</summary>
        protected virtual UniTask OnOpenAsync(object context, CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
#endif

        /// <summary>이미 열려 있을 때 같은 UI를 다시 열면 호출됩니다.</summary>
        protected virtual void OnUpdate(object context)
        {
        }

        /// <summary>닫을 때 호출됩니다.</summary>
        protected virtual void OnClose()
        {
        }

#if UNITASK_INSTALLED
        /// <summary>비동기로 닫을 때 호출됩니다.</summary>
        protected virtual UniTask OnCloseAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
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
                OnBeforeVisible();
            else
                OnBeforeHidden();

            EnsureCanvasGroup();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            if (!visible && immediate)
                gameObject.SetActive(false);
        }
    }
}
