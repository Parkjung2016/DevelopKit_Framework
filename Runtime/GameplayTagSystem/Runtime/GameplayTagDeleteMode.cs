namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>
    /// JSON 소스 파일에서 태그를 삭제할 때의 동작 방식입니다.
    /// </summary>
    public enum GameplayTagDeleteMode
    {
        /// <summary>태그만 삭제하고, 같은 파일의 자식 태그는 접두어를 제거해 승격합니다.</summary>
        TagOnly,

        /// <summary>태그와 같은 파일의 하위 태그를 모두 삭제합니다.</summary>
        Hierarchy
    }
}
