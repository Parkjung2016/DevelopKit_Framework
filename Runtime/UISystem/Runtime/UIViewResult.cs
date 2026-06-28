namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>뷰 표시·닫기 결과입니다.</summary>
    public readonly struct UIViewResult
    {
        public static UIViewResult Failed(string reason) => new(false, default, reason);

        public static UIViewResult Succeeded(IUIView view) => new(true, new UIViewHandle(view), null);

        public readonly bool IsSuccess;
        public readonly UIViewHandle Handle;
        public readonly string ErrorMessage;

        public UIViewResult(bool success, UIViewHandle handle, string errorMessage)
        {
            IsSuccess = success;
            Handle = handle;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>특정 뷰 타입에 대한 표시·닫기 결과입니다.</summary>
    public readonly struct UIViewResult<T> where T : UIViewBase
    {
        public static UIViewResult<T> Failed(string reason) => new(false, default, reason);

        public static UIViewResult<T> Succeeded(T view) => new(true, new UIViewHandle<T>(view), null);

        public readonly bool IsSuccess;
        public readonly UIViewHandle<T> Handle;
        public readonly string ErrorMessage;

        public UIViewResult(bool success, UIViewHandle<T> handle, string errorMessage)
        {
            IsSuccess = success;
            Handle = handle;
            ErrorMessage = errorMessage;
        }

        public static implicit operator UIViewResult(UIViewResult<T> result) =>
            result.IsSuccess
                ? UIViewResult.Succeeded(result.Handle.View)
                : UIViewResult.Failed(result.ErrorMessage);
    }
}
