using System.Collections.Generic;
#if UNITASK_INSTALLED
using Cysharp.Threading.Tasks;
#endif

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>Screen 레이어 화면 전환 스택입니다.</summary>
    internal sealed partial class UINavigationStack
    {
        private readonly List<UIScreenBase> screens = new();

        public int Count => screens.Count;

        public UIScreenBase Peek => screens.Count > 0 ? screens[^1] : null;

        public void Push(UIScreenBase screen, object context = null)
        {
            if (screen == null)
                return;

            UIScreenBase current = Peek;
            if (current != null && current != screen)
                UIViewLifecycle.Hide(current);

            if (!screens.Contains(screen))
                screens.Add(screen);
            else
            {
                screens.Remove(screen);
                screens.Add(screen);
            }

            UIViewLifecycle.Show(screen, context);
        }

        public bool TryPop()
        {
            if (screens.Count == 0)
                return false;

            UIScreenBase top = screens[^1];
            UIViewLifecycle.Hide(top);
            PopSilently();
            return true;
        }

        internal void PopSilently()
        {
            if (screens.Count == 0)
                return;

            screens.RemoveAt(screens.Count - 1);
            if (Peek != null)
                UIViewLifecycle.Show(Peek);
        }

        public void Remove(UIScreenBase screen, bool hide = true)
        {
            if (screen == null || !screens.Contains(screen))
                return;

            bool wasTop = ReferenceEquals(Peek, screen);
            if (hide)
                UIViewLifecycle.Hide(screen);

            screens.Remove(screen);

            if (wasTop && Peek != null)
                UIViewLifecycle.Show(Peek);
        }

        public void Clear(bool immediate = false)
        {
            for (int i = screens.Count - 1; i >= 0; i--)
                UIViewLifecycle.Hide(screens[i], immediate);

            screens.Clear();
        }

        internal void DrainTo(List<UIScreenBase> buffer, bool immediate = false)
        {
            buffer.Clear();
            for (int i = screens.Count - 1; i >= 0; i--)
                UIViewLifecycle.Hide(screens[i], immediate);

            buffer.AddRange(screens);
            screens.Clear();
        }

        internal void CopyTo(List<UIScreenBase> buffer)
        {
            buffer.Clear();
            buffer.AddRange(screens);
        }
    }
}
