namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>에디터 UI에 표시할 문자열(라벨·메시지·다이얼로그)을 모아 둡니다.</summary>
    internal static class GameplayTagEditorLocalization
    {
        // 필드 라벨 (에디터 UI)
        public const string WindowTitle = "Gameplay Tags";
        public const string TagDetails = "Tag Details";
        public const string AddNewTag = "Add New Tag";
        public const string SourceFile = "Source File";
        public const string FileName = "File Name";
        public const string Parent = "Parent";
        public const string Name = "Name";
        public const string Comment = "Comment";
        public const string FullName = "Full Name";
        public const string NameLabel = "Label";
        public const string Description = "Description";
        public const string Children = "Children";
        public const string Source = "Source";
        public const string Root = "(Root)";
        public const string None = "(none)";
        public const string Leaf = "(leaf)";
        public const string NewFile = "New File";
        public const string NoSourceFiles = "(없음)";
        public const string AutoCreate = "자동 생성";
        public const string TreeLocationRoot = "루트에 '{0}'(으)로 표시됩니다.";
        public const string TreeLocationUnderParent = "'{0}' 아래에 '{1}'(으)로 표시됩니다.";
        public const string TagCrossFileMoveTitle = "다른 소스 파일";
        public const string TagCrossFileMove = "옮기기";
        public const string TagCrossFileMoveMessage =
            "'{0}' 태그가 '{1}'에 있습니다.\n'{2}'(으)로 옮기고 계속할까요?";
        public const string TagCrossFileMoveMessageMulti =
            "다음 태그가 다른 소스 파일에 있습니다:\n{0}\n\n'{1}'(으)로 옮기고 계속할까요?";
        public const string TagCrossFileRenameConflictMessage =
            "'{0}' 태그가 '{1}'에 이미 있습니다.\n'{2}'에서 사용하려면 다른 파일의 태그가 제거됩니다.\n계속할까요?";
        public const string CreateMissingParentsTitle = "부모 태그 생성";
        public const string CreateMissingParentsMessage =
            "다음 부모 태그가 '{0}' 파일에 없습니다:\n{1}\n\n생성하고 계속할까요?";
        public const string TagEditInvalidParent = "자기 자신이나 자식 태그를 부모로 지정할 수 없습니다.";

        // 액션
        public const string GenerateTagScripts = "태그 스크립트 생성";
        public const string GenerateTagScriptsTooltip =
            "GameplayTags.Generated.cs를 갱신합니다. 태그 추가·수정 후 수동으로 실행하세요.";
        public const string TagScriptGenerateSuccess = "GameplayTags.Generated.cs 스크립트를 생성했습니다.";
        public const string TagScriptGenerateSkipped = "태그 스크립트 변경 사항이 없습니다.";
        public const string Refresh = "새로고침";
        public const string AddTag = "태그 추가";
        public const string OpenFolder = "폴더 열기";
        public const string DeleteSelected = "선택 삭제";
        public const string DeleteSelectedTooltip = "Delete: 삭제 | Shift+Delete: 하위 포함 삭제";
        public const string DeleteHierarchy = "하위 포함 삭제";
        public const string DeleteSourceFile = "소스 파일 삭제";
        public const string AddChild = "자식 추가";
        public const string Delete = "삭제";
        public const string Create = "생성";
        public const string Save = "저장";
        public const string TagNotInFile = "소스 파일에 '{0}' 태그가 없습니다.";
        public const string TagRenameConflict = "'{0}'(으)로 바꾸면 '{1}' 태그와 이름이 겹칩니다.";
        public const string TagEditReadOnly = "이 태그는 소스 파일에서 편집할 수 없습니다.";
        public const string Cancel = "취소";
        public const string Ok = "확인";

        public const string SelectTagPrompt = "태그를 선택하세요.";
        public const string MultiSelectPrompt = "{0}개 태그 선택됨";
        public const string NoTagsFound = "태그가 없습니다.";
        public const string NoTagsInSourceFile = "이 소스 파일에 태그가 없습니다.";
        public const string NoSourceFilesPrompt = "태그 소스 파일이 없습니다. '태그 추가'로 새 JSON 파일을 만드세요.";
        public const string NameTooltip =
            "이름(Jump) 또는 전체 경로(Ability.Jump)를 입력하세요. 없는 부모는 선택한 소스 파일에 자동 생성됩니다.";

        public const string DeleteTagTitle = "태그 삭제";
        public const string DeleteTagsTitle = "태그 삭제";
        public const string DeleteTagMessage = "'{0}' 태그를 삭제할까요?";
        public const string DeleteTagPromoteMessage =
            "'{0}' 태그를 삭제할까요?\n같은 파일의 자식 태그에서 '{0}.' 접두어가 제거됩니다.";
        public const string DeleteTagHierarchyMessage =
            "'{0}' 태그와 같은 파일의 하위 태그 {1}개를 모두 삭제할까요?";
        public const string DeleteTagsMessage = "선택한 {0}개 태그를 삭제할까요?";
        public const string DeleteTagsPromoteMessage =
            "선택한 {0}개 태그를 삭제할까요?\n자식이 있는 태그는 같은 파일에서 이름 접두어가 제거됩니다.";
        public const string DeleteTagsHierarchyMessage =
            "선택한 {0}개 태그와 하위 태그를 모두 삭제할까요?";
        public const string DeleteSourceFileTitle = "소스 파일 삭제";
        public const string DeleteSourceFileMessage = "소스 파일 '{0}'과 포함된 태그를 모두 삭제할까요?";
        public const string SelectSourceFileToDelete = "삭제할 소스 파일을 선택하세요.";
        public const string SourceFileNotFound = "파일을 찾을 수 없습니다.";

        public const string PickerSelectTag = "Select Tag";
        public const string PickerEditTags = "Edit Tags";
        public const string PickerFilter = "Filter: {0}";
        public const string PickerClearAll = "전체 해제";
        public const string PickerNone = "없음";
        public const string PickerManager = "관리자";
        public const string PickerDescriptionPrompt = "태그에 마우스를 올리면 설명이 표시됩니다.";
        public const string PickerCloseHint = "Esc: 닫기";

        public const string DrawerSelectTag = "태그 선택...";
        public const string DrawerInvalidTag = "잘못된 태그: {0}";
        public const string DrawerInvalidTagTooltip = "태그가 없거나 이름이 변경되었습니다.";
        public const string DrawerSelectTagTooltip = "클릭하여 태그를 선택합니다.";
        public const string DrawerEditTags = "태그 편집";
        public const string DrawerEditTagsTooltip = "태그 선택 창을 엽니다.";
        public const string DrawerNoTags = "태그 없음";
        public const string DrawerMixedValues = "선택된 항목마다 값이 다릅니다.";
        public const string DrawerRemoveTagTooltip = "태그 제거";
        public const string DrawerInvalidSuffix = " (잘못됨)";

        public const string TagCreateReloadFailed = "태그는 생성됐지만 다시 불러오지 못했습니다.";
        public const string TagAlreadyExists = "'{0}' 태그가 '{1}' 파일에 이미 있습니다.";
        public const string TagSourceNotLoaded = "태그 소스 파일을 불러오지 못했습니다.";
        public const string InvalidTagNameDetail = "태그 이름이 올바르지 않습니다.";
        public const string DeletePromoteConflict =
            "'{0}' 태그를 삭제할 수 없습니다. 자식을 올리면 '{1}' 태그와 이름이 겹칩니다. Shift+Delete로 하위까지 삭제하세요.";
        public const string SourceFileNotFoundInList = "선택한 소스 파일을 찾을 수 없습니다. 목록을 새로고침하세요.";
        public const string SourceFileCreateFailed = "태그 소스 파일을 만들지 못했습니다.";
        public const string ParentFileMismatch =
            "부모 태그는 같은 소스 파일에 있거나, 새 태그의 직접 부모와 일치해야 합니다.";
        public const string SourceFileNameRequired = "파일 이름을 입력하세요.";
        public const string SourceFileAlreadyExists =
            "파일 '{0}'이(가) 이미 있습니다. 소스 파일 목록에서 선택하거나 다른 이름을 쓰세요.";
        public const string SegmentNameRequired = "태그 이름을 입력하세요.";
        public const string SegmentUseParentOrFullPath = "이름만 입력하거나, (Root)에서 Ability.Jump처럼 전체 경로를 입력하세요.";
        public const string SegmentInvalidCharacter = "사용할 수 없는 문자 '{0}'입니다. 영문, 숫자, 밑줄만 가능합니다.";
        public const string PlayModeReloadWarning =
            "플레이 모드 중 태그가 다시 로드되었습니다. Enter Play Mode Options를 끄거나 도메인 리로드를 실행하세요.";
    }
}
