using System.Collections.Generic;

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
                current.Hide();

            if (!screens.Contains(screen))
                screens.Add(screen);
            else
            {
                screens.Remove(screen);
                screens.Add(screen);
            }

            screen.Show(context);
        }

        public bool TryPop()
        {
            if (screens.Count == 0)
                return false;

            UIScreenBase top = screens[^1];
            top.Hide();
            PopSilently();
            return true;
        }

        internal void PopSilently()
        {
            if (screens.Count == 0)
                return;

            screens.RemoveAt(screens.Count - 1);
            Peek?.Show();
        }

        public void Remove(UIScreenBase screen, bool hide = true)
        {
            if (screen == null || !screens.Contains(screen))
                return;

            bool wasTop = ReferenceEquals(Peek, screen);
            if (hide)
                screen.Hide();

            screens.Remove(screen);

            if (wasTop)
                Peek?.Show();
        }

        public void Clear(bool immediate = false)
        {
            for (int i = screens.Count - 1; i >= 0; i--)
                screens[i].Hide(immediate);

            screens.Clear();
        }
    }
}
