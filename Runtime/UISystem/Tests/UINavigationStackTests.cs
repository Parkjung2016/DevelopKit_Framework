using System.Threading;
#if UNITASK_INSTALLED
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
#endif
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
        public void Hide_WhenAlreadyHidden_DoesNotCallCloseLifecycle()
        {
            MockScreen screen = CreateScreen();

            UIViewLifecycle.Hide(screen, immediate: true);

            Assert.AreEqual(0, screen.HideCount);
            Assert.AreEqual(UIViewState.Hidden, screen.State);
        }

        [Test]
        public void DrainTo_ReusesBufferAndClearsStack()
        {
            UINavigationStack stack = new();
            MockScreen first = CreateScreen();
            MockScreen second = CreateScreen();
            var buffer = new System.Collections.Generic.List<UIScreenBase> { null };
            stack.Push(first);
            stack.Push(second);

            stack.DrainTo(buffer, immediate: true);

            Assert.AreEqual(0, stack.Count);
            Assert.AreEqual(2, buffer.Count);
            Assert.AreSame(first, buffer[0]);
            Assert.AreSame(second, buffer[1]);
            Assert.AreEqual(1, first.HideCount);
            Assert.AreEqual(1, second.HideCount);
        }
#if UNITASK_INSTALLED
        [Test]
        public async Task HideDuringShow_LateOpenCannotMakeViewVisibleAgain()
        {
            GameObject gameObject = new GameObject("DelayedScreen");
            createdObjects.Add(gameObject);
            DelayedScreen screen = gameObject.AddComponent<DelayedScreen>();

            UniTask showTask = screen.Show();
            Assert.AreEqual(UIViewState.Showing, screen.State);

            await screen.Hide(immediate: true).AsTask();
            Assert.AreEqual(UIViewState.Hidden, screen.State);

            screen.CompleteOpen();
            await showTask.AsTask();

            Assert.AreEqual(UIViewState.Hidden, screen.State);
            Assert.IsFalse(screen.gameObject.activeSelf);
            Assert.AreEqual(1, screen.OpenCount);
            Assert.AreEqual(1, screen.CloseCount);
        }
#endif
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

#if UNITASK_INSTALLED
        private sealed class DelayedScreen : UIScreenBase
        {
            private readonly UniTaskCompletionSource openCompletion = new();

            public int OpenCount { get; private set; }
            public int CloseCount { get; private set; }

            public void CompleteOpen() => openCompletion.TrySetResult();

            protected override UniTask OnOpen(
                object context,
                CancellationToken cancellationToken = default)
            {
                OpenCount++;
                return openCompletion.Task;
            }

            protected override UniTask OnClose(CancellationToken cancellationToken = default)
            {
                CloseCount++;
                return UniTask.CompletedTask;
            }
        }
#endif
        private sealed class MockScreen : UIScreenBase
        {
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }

#if UNITASK_INSTALLED
            protected override UniTask OnOpen(object context, CancellationToken cancellationToken = default)
            {
                ShowCount++;
                return UniTask.CompletedTask;
            }

            protected override UniTask OnClose(CancellationToken cancellationToken = default)
            {
                HideCount++;
                return UniTask.CompletedTask;
            }
#else
            protected override void OnOpen(object context)
            {
                ShowCount++;
            }

            protected override void OnClose()
            {
                HideCount++;
            }
#endif
        }
    }
}