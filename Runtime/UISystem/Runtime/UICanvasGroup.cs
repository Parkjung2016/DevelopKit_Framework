namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>
    /// 물리 Canvas 묶음입니다. 레이어 ID(Screen, Popup…)와 별개로,
    /// 어떤 Canvas에 붙일지와 리빌드·입력 범위를 나눕니다.
    /// </summary>
    public enum UICanvasGroup
    {
        /// <summary>본체 UI Canvas. Screen·Overlay 레이어.</summary>
        Main = 0,

        /// <summary>떠 있는 UI Canvas. Popup·Modal 레이어.</summary>
        Floating = 100,

        /// <summary>시스템 UI Canvas. 로딩·연결 끊김 등.</summary>
        System = 200
    }
}
