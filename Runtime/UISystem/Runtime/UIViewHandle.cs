#if UNITASK_INSTALLED
using Cysharp.Threading.Tasks;
#endif

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>표시 중인 뷰에 대한 핸들입니다.</summary>
    public readonly struct UIViewHandle
    {
        public static UIViewHandle Invalid => default;

        public readonly IUIView View;
        public readonly bool IsValid => View != null;

        public UIViewHandle(IUIView view) => View = view;

        public bool TryGetView<T>(out T view) where T : UIViewBase
        {
            view = View as T;
            return view != null;
        }

        public void Close(bool immediate = false)
        {
            if (!IsValid)
                return;

            UIManager.Instance.Close(View, immediate);
        }

#if UNITASK_INSTALLED
        public UniTask CloseAsync(bool immediate = false, System.Threading.CancellationToken cancellationToken = default)
        {
            if (!IsValid)
                return UniTask.CompletedTask;

            return UIManager.Instance.CloseAsync(View, immediate, cancellationToken);
        }
#endif
    }

    /// <summary>특정 뷰 타입에 대한 핸들입니다.</summary>
    public readonly struct UIViewHandle<T> where T : UIViewBase
    {
        public static UIViewHandle<T> Invalid => default;

        public readonly T View;
        public readonly bool IsValid => View != null;

        public UIViewHandle(T view) => View = view;

        public void Close(bool immediate = false)
        {
            if (!IsValid)
                return;

            UIManager.Instance.Close(View, immediate);
        }

#if UNITASK_INSTALLED
        public UniTask CloseAsync(bool immediate = false, System.Threading.CancellationToken cancellationToken = default)
        {
            if (!IsValid)
                return UniTask.CompletedTask;

            return UIManager.Instance.CloseAsync(View, immediate, cancellationToken);
        }
#endif
    }
}
