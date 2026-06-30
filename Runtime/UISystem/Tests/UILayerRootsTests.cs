using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Tests
{
    [TestFixture]
    public sealed class UILayerRootsTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(createdObjects[i]);

            createdObjects.Clear();
        }

        [Test]
        public void EnsureDefaults_CreatesScreensRootForBuiltInScreenLayer()
        {
            UILayerRegistry registry = new();
            registry.Initialize(UILayerSettings.CreateBuiltIn());

            UILayerRoots layerRoots = CreateLayerRoots();
            layerRoots.EnsureDefaults(registry);

            RectTransform screenRoot = layerRoots.GetRoot(UILayers.Screen, registry);
            Assert.NotNull(screenRoot);
            Assert.AreEqual("Screens", screenRoot.name);
        }

        [Test]
        public void EnsureDefaults_RestoresMissingScreenLayerInSettings()
        {
            UILayerSettings settings = UILayerSettings.CreateBuiltIn();
            List<UILayerDefinition> layers = GetLayers(settings);
            layers.RemoveAll(layer => layer.LayerId == UILayers.Screen);

            settings.EnsureDefaults();

            Assert.IsTrue(layers.Exists(layer => layer.LayerId == UILayers.Screen));
        }

        [Test]
        public void GetRoot_UsesScreensRootWhenScreenLayerExists()
        {
            UILayerSettings settings = UILayerSettings.CreateBuiltIn();
            List<UILayerDefinition> layers = GetLayers(settings);
            UILayerDefinition overlay = layers.Find(layer => layer.LayerId == UILayers.Overlay);
            overlay.SetUseScreenStack(true);

            settings.EnsureDefaults();

            UILayerRegistry registry = new();
            registry.Initialize(settings);

            UILayerRoots layerRoots = CreateLayerRoots();
            layerRoots.EnsureDefaults(registry);

            RectTransform screenRoot = layerRoots.GetRoot(UILayers.Screen, registry);
            Assert.NotNull(screenRoot);
            Assert.AreEqual("Screens", screenRoot.name);
            Assert.IsFalse(registry.TryGet(UILayers.Overlay, out UILayerDefinition overlayDefinition)
                && overlayDefinition.UseScreenStack);
        }

        [Test]
        public void Registry_PrefersBuiltInScreenLayerWhenMultipleUseScreenStack()
        {
            UILayerSettings settings = UILayerSettings.CreateBuiltIn();
            List<UILayerDefinition> layers = GetLayers(settings);
            layers.RemoveAll(layer => layer.LayerId == UILayers.Screen);
            layers.Find(layer => layer.LayerId == UILayers.Overlay)?.SetUseScreenStack(true);

            UILayerRegistry registry = new();
            registry.Initialize(settings, ensureDefaults: false);

            Assert.AreEqual(UILayers.Overlay, registry.ScreenLayerId);

            settings.EnsureDefaults();
            registry.Initialize(settings);

            Assert.AreEqual(UILayers.Screen, registry.ScreenLayerId);
        }

        private UILayerRoots CreateLayerRoots()
        {
            GameObject rootObject = new("UI Root", typeof(RectTransform), typeof(UILayerRoots));
            createdObjects.Add(rootObject);
            return rootObject.GetComponent<UILayerRoots>();
        }

        private static List<UILayerDefinition> GetLayers(UILayerSettings settings)
        {
            FieldInfo field = typeof(UILayerSettings).GetField(
                "layers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<UILayerDefinition>)field.GetValue(settings);
        }
    }
}
