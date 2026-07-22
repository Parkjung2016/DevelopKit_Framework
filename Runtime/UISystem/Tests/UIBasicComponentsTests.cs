using NUnit.Framework;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Tests
{
    [TestFixture]
    public sealed class UIBasicComponentsTests
    {
        private GameObject owner;

        [TearDown]
        public void TearDown()
        {
            if (owner != null)
                Object.DestroyImmediate(owner);
        }

        [Test]
        public void ProgressBar_NormalizedValueUsesConfiguredRange()
        {
            owner = new GameObject("ProgressBarTest");
            UIProgressBar progressBar = owner.AddComponent<UIProgressBar>();

            progressBar.SetRange(10f, 30f);
            progressBar.SetValue(20f, animate: false);

            Assert.AreEqual(0.5f, progressBar.NormalizedValue, 0.0001f);
            Assert.AreEqual(20f, progressBar.Value, 0.0001f);
        }

        [Test]
        public void ProgressBar_ClampsValuesToRange()
        {
            owner = new GameObject("ProgressBarTest");
            UIProgressBar progressBar = owner.AddComponent<UIProgressBar>();

            progressBar.SetRange(0f, 100f);
            progressBar.SetValue(150f, animate: false);

            Assert.AreEqual(100f, progressBar.Value, 0.0001f);
            Assert.AreEqual(1f, progressBar.NormalizedValue, 0.0001f);
        }

        [Test]
        public void LoadingRequest_ClampsInitialProgress()
        {
            var request = new LoadingRequest("Loading", progress: 2f, isIndeterminate: false);

            Assert.AreEqual(1f, request.Progress);
            Assert.IsFalse(request.IsIndeterminate);
            Assert.IsTrue(request.BlockInput);
        }

        [Test]
        public void ToastRequest_UsesMessageAsDefaultDuplicateKey()
        {
            var first = new ToastRequest("Saved", ToastType.Success);
            var second = new ToastRequest("Saved", ToastType.Success);
            var warning = new ToastRequest("Saved", ToastType.Warning);

            Assert.AreEqual(first.GetDuplicateKey(), second.GetDuplicateKey());
            Assert.AreNotEqual(first.GetDuplicateKey(), warning.GetDuplicateKey());
        }
    }
}
