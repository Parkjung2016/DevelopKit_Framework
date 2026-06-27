namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>Back 입력 시 이 뷰가 어떻게 동작하는지 정의합니다.</summary>
    public enum UIViewBackBehavior
    {
        /// <summary>Back 키로 이 뷰를 닫습니다.</summary>
        CloseOnBack = 0,

        /// <summary>Back 키를 OnBack()으로만 넘깁니다.</summary>
        HandleManually = 1,

        /// <summary>Back 대상에서 빼 둡니다. 뒤에 열린 UI가 처리합니다.</summary>
        PassThrough = 2
    }
}
