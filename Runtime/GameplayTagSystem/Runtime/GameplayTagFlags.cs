using System;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>게임플레이 태그에 설정할 수 있는 플래그입니다.</summary>
    public enum GameplayTagFlags
    {
        None = 0,

        [Obsolete("It should be removed soon")]
        HideInEditor = 1 << 0,
    }
}
