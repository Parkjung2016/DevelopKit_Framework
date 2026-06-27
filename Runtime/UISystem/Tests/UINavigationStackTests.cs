using NUnit.Framework;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Tests
{
    [TestFixture]
    public sealed class UINavigationStackTests
    {
        private readonly System.Collections.Generic.List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(createdObjects[i]);

            createdObjects.Clear();
        }

        [Test]
        public void PushAndPop_ManagesTopScreen()
        {
            UINavigationStack stack = new();
            MockScreen first = CreateScreen();
            MockScreen second = CreateScreen();

            stack.Push(first);
            stack.Push(second);

            Assert.AreEqual(second, stack.Peek);
            Assert.AreEqual(1, first.HideCount);

            Assert.IsTrue(stack.TryPop());
            Assert.AreEqual(first, stack.Peek);
            Assert.AreEqual(2, first.ShowCount);
        }

        private MockScreen CreateScreen()
        {
            GameObject gameObject = new GameObject("MockScreen");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<MockScreen>();
        }

        private sealed class MockScreen : UIScreenBase
        {
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }

            protected override void OnOpen(object context) => ShowCount++;

            protected override void OnClose() => HideCount++;
        }
    }
}
