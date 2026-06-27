using System.Collections.Generic;
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

        public void Show(object context = null) => State = UIViewState.Shown;

        public void Hide(bool immediate = false) => State = UIViewState.Hidden;

        public bool HandleBack()
        {
            BackCallCount++;
            if (!HandleBackResult)
                return false;

            Hide();
            return true;
        }
    }

    internal static class MockUIViewList
    {
        public static List<IUIView> Create(params MockUIView[] views) => new List<IUIView>(views);
    }
}
