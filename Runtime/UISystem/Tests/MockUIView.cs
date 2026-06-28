using System.Collections.Generic;
using System.Threading;
#if UNITASK_INSTALLED
using Cysharp.Threading.Tasks;
#endif
using PJDev.DevelopKit.Framework.UISystem.Runtime;

namespace PJDev.DevelopKit.Framework.UISystem.Tests
{
    internal sealed class MockUIView : IUIView
    {
        public string ViewId { get; set; }
        public string LayerId { get; set; } = UILayers.Popup;
        public int Priority { get; set; }
        public UIViewState State { get; private set; } = UIViewState.Hidden;
        public bool IsVisible => State is UIViewState.Showing or UIViewState.Shown;
        public UIViewBackBehavior BackBehavior { get; set; } = UIViewBackBehavior.CloseOnBack;
        public bool CloseOnBack => BackBehavior == UIViewBackBehavior.CloseOnBack;
        public bool BlocksBack => BackBehavior != UIViewBackBehavior.PassThrough;


        public int BackCallCount { get; private set; }
        public bool HandleBackResult { get; set; } = true;

#if UNITASK_INSTALLED
        public UniTask Show(object context = null, CancellationToken cancellationToken = default)
        {
            State = UIViewState.Shown;
            return UniTask.CompletedTask;
        }

        public UniTask Hide(bool immediate = false, CancellationToken cancellationToken = default)
        {
            State = UIViewState.Hidden;
            return UniTask.CompletedTask;
        }
#else
        public void Show(object context = null)
        {
            State = UIViewState.Shown;
        }

        public void Hide(bool immediate = false)
        {
            State = UIViewState.Hidden;
        }

#endif

        public bool HandleBack()
        {
            BackCallCount++;
            if (!HandleBackResult)
                return false;

            UIViewLifecycle.Hide(this);
            return true;
        }
    }

    internal static class MockUIViewList
    {
        public static List<IUIView> Create(params MockUIView[] views) => new List<IUIView>(views);
    }
}