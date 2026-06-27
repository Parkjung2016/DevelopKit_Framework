using NUnit.Framework;
using PJDev.DevelopKit.Framework.UISystem.Runtime;

namespace PJDev.DevelopKit.Framework.UISystem.Tests
{
    public sealed class UILayerUtilityTests
    {
        private UILayerRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new UILayerRegistry();
            registry.Initialize(UILayerSettings.CreateBuiltIn());
        }

        [Test]
        public void GetCanvasGroupId_MapsMainLayers()
        {
            Assert.AreEqual(UICanvasGroups.Main, UILayerUtility.GetCanvasGroupId(UILayers.Screen, registry));
            Assert.AreEqual(UICanvasGroups.Main, UILayerUtility.GetCanvasGroupId(UILayers.Overlay, registry));
        }

        [Test]
        public void GetCanvasGroupId_MapsFloatingLayers()
        {
            Assert.AreEqual(UICanvasGroups.Floating, UILayerUtility.GetCanvasGroupId(UILayers.Popup, registry));
            Assert.AreEqual(UICanvasGroups.Floating, UILayerUtility.GetCanvasGroupId(UILayers.Modal, registry));
        }

        [Test]
        public void GetCanvasGroupId_MapsSystemLayer()
        {
            Assert.AreEqual(UICanvasGroups.System, UILayerUtility.GetCanvasGroupId(UILayers.System, registry));
        }

        [Test]
        public void GetLayerIdsInGroup_ReturnsExpectedLayers()
        {
            var buffer = new System.Collections.Generic.List<string>();
            UILayerUtility.GetLayerIdsInGroup(UICanvasGroups.Floating, registry, buffer);

            Assert.Contains(UILayers.Popup, buffer);
            Assert.Contains(UILayers.Modal, buffer);
        }

        [Test]
        public void Registry_ExposesBuiltInSortOrderAndScreenLayer()
        {
            UILayerSettings settings = UILayerSettings.CreateBuiltIn();
            registry.Initialize(settings);

            Assert.AreEqual(300, registry.GetSortOrder(UILayers.Modal));
            Assert.IsTrue(registry.IsScreenLayer(UILayers.Screen));
            Assert.IsTrue(registry.TryGetCanvasGroup(UICanvasGroups.Main, out _));
        }
    }
}
