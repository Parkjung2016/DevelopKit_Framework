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
}
