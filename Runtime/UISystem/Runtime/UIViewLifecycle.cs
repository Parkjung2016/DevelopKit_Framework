#if UNITASK_INSTALLED
using Cysharp.Threading.Tasks;
#endif

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>동기 Manager API에서 View 수명주기를 일관되게 호출합니다.</summary>
    internal static class UIViewLifecycle
    {
#if UNITASK_INSTALLED
        public static void Show(IUIView view, object context = null) => view.Show(context).Forget();
        public static void Hide(IUIView view, bool immediate = false) => view.Hide(immediate).Forget();
#else
        public static void Show(IUIView view, object context = null) => view.Show(context);
        public static void Hide(IUIView view, bool immediate = false) => view.Hide(immediate);
#endif
    }
}
