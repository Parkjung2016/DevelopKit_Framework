using NUnit.Framework;
using PJDev.DevelopKit.Framework.UISystem.Runtime;

namespace PJDev.DevelopKit.Framework.UISystem.Tests
{
    [TestFixture]
    public sealed class UIViewSortUtilityTests
    {
        private UILayerRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new UILayerRegistry();
            registry.Initialize(UILayerSettings.CreateBuiltIn());
        }

        [Test]
        public void CompareForBack_HigherLayerComesFirst()
        {
            MockUIView popup = new() { LayerId = UILayers.Popup, Priority = 0 };
            MockUIView modal = new() { LayerId = UILayers.Modal, Priority = 0 };

            int compare = UIViewSortUtility.CompareForBack(popup, modal, MockUIViewList.Create(popup, modal), registry);

            Assert.Greater(compare, 0);
        }

        [Test]
        public void CompareForBack_HigherPriorityComesFirstWithinSameLayer()
        {
            MockUIView low = new() { LayerId = UILayers.Popup, Priority = 1 };
            MockUIView high = new() { LayerId = UILayers.Popup, Priority = 10 };

            int compare = UIViewSortUtility.CompareForBack(low, high, MockUIViewList.Create(low, high), registry);

            Assert.Greater(compare, 0);
        }
    }
}
