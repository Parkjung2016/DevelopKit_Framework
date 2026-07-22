using System.Collections.Generic;
#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public class UIToastView : UIViewBase
    {
        private sealed class ActiveToast
        {
            public UIToastItem Item;
            public ToastRequest Request;
            public float Elapsed;
            public float Duration;
        }

        [SerializeField] private RectTransform container;
        [SerializeField] private UIToastItem itemPrefab;
        [SerializeField] private ToastDisplayMode displayMode = ToastDisplayMode.Queue;
        [SerializeField, Min(1)] private int maxVisible = 3;
        [SerializeField, Min(0.01f)] private float defaultDuration = 2f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.12f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.18f;
        [SerializeField] private bool suppressDuplicates = true;
        [SerializeField] private Color defaultBackgroundColor = new(0.12f, 0.12f, 0.12f, 0.95f);
        [SerializeField] private Color defaultTextColor = Color.white;
        [SerializeField] private List<UIToastStyle> styles = new();

        private readonly Queue<ToastRequest> pending = new();
        private readonly List<ActiveToast> active = new();
        private readonly Stack<UIToastItem> itemPool = new();
        private bool closeRequested;

        public int PendingCount => pending.Count;
        public int VisibleCount => active.Count;

        protected override string ResolveDefaultLayerId() => UILayers.System;
        protected override bool InteractableWhenVisible => false;
        protected override bool BlocksRaycastsWhenVisible => false;

        protected override void Awake()
        {
            base.Awake();
            if (itemPrefab != null && itemPrefab.transform.IsChildOf(transform))
                itemPrefab.gameObject.SetActive(false);
        }

#if UNITASK_INSTALLED
        protected override UniTask OnOpen(object context, CancellationToken cancellationToken = default)
        {
            OpenOrUpdate(context);
            return UniTask.CompletedTask;
        }
#else
        protected override void OnOpen(object context) => OpenOrUpdate(context);
#endif

        protected override void OnUpdate(object context) => OpenOrUpdate(context);

        protected virtual void Update()
        {
            if (active.Count == 0)
            {
                FillVisibleSlots();
                CloseWhenEmpty();
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveToast toast = active[i];
                toast.Elapsed += deltaTime;
                toast.Item.SetAlpha(EvaluateAlpha(toast));
                if (toast.Elapsed < toast.Duration)
                    continue;

                Release(toast.Item);
                active.RemoveAt(i);
            }

            FillVisibleSlots();
            CloseWhenEmpty();
        }

        public virtual bool Enqueue(in ToastRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message) || itemPrefab == null)
                return false;

            closeRequested = false;
            if (suppressDuplicates && TryRefreshDuplicate(request))
                return true;

            if (displayMode == ToastDisplayMode.Replace)
            {
                pending.Clear();
                ReleaseAllActive();
            }

            pending.Enqueue(request);
            FillVisibleSlots();
            return true;
        }

        protected override void OnBeforeHidden()
        {
            pending.Clear();
            ReleaseAllActive();
            closeRequested = false;
            base.OnBeforeHidden();
        }

        protected virtual UIToastItem CreateItem()
        {
            RectTransform parent = container != null ? container : transform as RectTransform;
            return Instantiate(itemPrefab, parent);
        }

        protected virtual UIToastStyle ResolveStyle(ToastType type)
        {
            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i].Type == type)
                    return styles[i];
            }

            return new UIToastStyle
            {
                Type = type,
                BackgroundColor = defaultBackgroundColor,
                TextColor = defaultTextColor
            };
        }

        private void OpenOrUpdate(object context)
        {
            if (context is ToastRequest request)
                Enqueue(request);
        }

        private void FillVisibleSlots()
        {
            int limit = displayMode == ToastDisplayMode.Queue ? 1 : Mathf.Max(1, maxVisible);
            while (active.Count < limit && pending.Count > 0)
            {
                ToastRequest request = pending.Dequeue();
                UIToastItem item = Acquire();
                if (item == null)
                    return;

                UIToastStyle style = ResolveStyle(request.Type);
                item.Show(request, style);
                active.Add(new ActiveToast
                {
                    Item = item,
                    Request = request,
                    Duration = request.Duration > 0f ? request.Duration : defaultDuration
                });
            }
        }

        private UIToastItem Acquire()
        {
            while (itemPool.Count > 0)
            {
                UIToastItem item = itemPool.Pop();
                if (item != null)
                    return item;
            }

            return CreateItem();
        }

        private void Release(UIToastItem item)
        {
            if (item == null)
                return;

            item.ResetItem();
            itemPool.Push(item);
        }

        private void ReleaseAllActive()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                Release(active[i].Item);
            active.Clear();
        }

        private bool TryRefreshDuplicate(in ToastRequest request)
        {
            string key = request.GetDuplicateKey();
            for (int i = 0; i < active.Count; i++)
            {
                ActiveToast toast = active[i];
                if (toast.Request.GetDuplicateKey() != key)
                    continue;

                toast.Elapsed = 0f;
                toast.Duration = request.Duration > 0f ? request.Duration : defaultDuration;
                return true;
            }

            foreach (ToastRequest queued in pending)
            {
                if (queued.GetDuplicateKey() == key)
                    return true;
            }

            return false;
        }

        private float EvaluateAlpha(ActiveToast toast)
        {
            float fadeIn = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(toast.Elapsed / fadeInDuration);
            float remaining = toast.Duration - toast.Elapsed;
            float fadeOut = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(remaining / fadeOutDuration);
            return Mathf.Min(fadeIn, fadeOut);
        }

        private void CloseWhenEmpty()
        {
            if (closeRequested || active.Count > 0 || pending.Count > 0)
                return;

            closeRequested = true;
            Close();
        }
    }
}
