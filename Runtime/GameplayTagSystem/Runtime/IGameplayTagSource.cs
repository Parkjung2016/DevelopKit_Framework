namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그 JSON 파일에서 이름·설명(Comment)을 편집합니다.</summary>
    internal interface IGameplayTagEditHandler
    {
        bool TryUpdateComment(string tagName, string comment, out string errorMessage);

        bool TryRenameTag(string oldName, string newName, out string errorMessage, bool createMissingParents = false);
    }

    /// <summary>태그 JSON 파일에서 태그 삭제를 처리합니다.</summary>
    internal interface IDeleteTagHandler
    {
        bool TryValidateDelete(string tagName, GameplayTagDeleteMode mode, out string errorMessage);

        bool TryDeleteTag(string tagName, GameplayTagDeleteMode mode, out string errorMessage);

        void DeleteTag(string tagName, GameplayTagDeleteMode mode = GameplayTagDeleteMode.TagOnly);
    }

    /// <summary>게임플레이 태그를 등록하는 소스(어셈블리, JSON, 빌드 파일 등)입니다.</summary>
    internal interface IGameplayTagSource
    {
        string Name { get; }

        void RegisterTags(GameplayTagRegistrationContext context);
    }
}
