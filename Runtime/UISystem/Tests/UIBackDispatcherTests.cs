using NUnit.Framework;
using PJDev.DevelopKit.Framework.UISystem.Runtime;

namespace PJDev.DevelopKit.Framework.UISystem.Tests
{
    [TestFixture]
    public sealed class UIBackDispatcherTests
    {
        private UILayerRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new UILayerRegistry();
            registry.Initialize(UILayerSettings.CreateBuiltIn());
        }

        [Test]
        public void TryHandleBack_ClosesTopModalBeforePopup()
        {
            UINavigationStack screens = new();
            MockUIView popup = new() { LayerId = UILayers.Popup, ViewId = "Popup" };
            MockUIView modal = new() { LayerId = UILayers.Modal, ViewId = "Modal" };
            popup.Show();
            modal.Show();

            bool handled = UIBackDispatcher.TryHandleBack(screens, MockUIViewList.Create(popup, modal), registry);

            Assert.IsTrue(handled);
            Assert.AreEqual(1, modal.BackCallCount);
            Assert.AreEqual(0, popup.BackCallCount);
        }

        [Test]
        public void TryHandleBack_WithNoVisibleViews_ReturnsFalse()
        {
            UINavigationStack screens = new();
            MockUIView hidden = new() { LayerId = UILayers.Popup };

            bool handled = UIBackDispatcher.TryHandleBack(screens, MockUIViewList.Create(hidden), registry);

            Assert.IsFalse(handled);
        }

        [Test]
        public void TryHandleBack_SkipsNonBlockingOverlay()
        {
            UINavigationStack screens = new();
            MockUIView overlay = new()
            {
                LayerId = UILayers.Overlay,
                BackBehavior = UIViewBackBehavior.PassThrough
            };
            overlay.Show();

            bool handled = UIBackDispatcher.TryHandleBack(screens, MockUIViewList.Create(overlay), registry);

            Assert.IsFalse(handled);
        }
    }
}
