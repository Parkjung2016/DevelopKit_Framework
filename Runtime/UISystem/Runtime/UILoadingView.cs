#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public class UILoadingView : UIViewBase
    {
        [SerializeField] private Text messageText;
        [SerializeField] private UIProgressBar progressBar;
        [SerializeField] private GameObject determinateRoot;
        [SerializeField] private GameObject indeterminateRoot;
        [SerializeField] private RectTransform spinner;
        [SerializeField] private float spinnerSpeed = 180f;
        [SerializeField] private Button cancelButton;
        [SerializeField] private bool cancelOnBack;

        private LoadingViewData data;

        public LoadingViewData Data => data;

        protected override string ResolveDefaultLayerId() => UILayers.System;
        protected override bool InteractableWhenVisible => data.BlockInput || data.CanCancel;
        protected override bool BlocksRaycastsWhenVisible => data.BlockInput || data.CanCancel;

#if UNITASK_INSTALLED
        protected override UniTask OnOpen(object context, CancellationToken cancellationToken = default)
        {
            ApplyContext(context);
            return UniTask.CompletedTask;
        }
#else
        protected override void OnOpen(object context) => ApplyContext(context);
#endif

        protected override void OnUpdate(object context) => ApplyContext(context);

        protected override void BindEvent()
        {
            base.BindEvent();
            if (cancelButton != null)
                BindButton(cancelButton, RequestCancel);
        }

        protected override bool OnBack()
        {
            if (cancelOnBack && data.CanCancel)
                data.Cancel.Invoke();

            return true;
        }

        protected virtual void Update()
        {
            if (spinner != null && data.IsIndeterminate)
                spinner.Rotate(0f, 0f, -spinnerSpeed * Time.unscaledDeltaTime);
        }

        public virtual void Apply(in LoadingViewData viewData)
        {
            data = viewData;

            if (messageText != null)
                messageText.text = data.Message;

            if (determinateRoot != null)
                determinateRoot.SetActive(!data.IsIndeterminate);
            if (indeterminateRoot != null)
                indeterminateRoot.SetActive(data.IsIndeterminate);

            if (progressBar != null && !data.IsIndeterminate)
                progressBar.SetNormalizedValue(data.Progress);

            if (cancelButton != null)
                cancelButton.gameObject.SetActive(data.CanCancel);

            RefreshInteractionState();
        }

        protected override void OnBeforeHidden()
        {
            data = default;
            base.OnBeforeHidden();
        }

        private void ApplyContext(object context)
        {
            if (context is LoadingViewData viewData)
                Apply(viewData);
        }

        private void RequestCancel()
        {
            if (data.CanCancel)
                data.Cancel.Invoke();
        }
    }
}
