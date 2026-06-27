namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>레거시 <see cref="UICanvasGroup"/> enum과 문자열 ID 변환입니다.</summary>
    public static class UICanvasGroupUtility
    {
        public static string EnumToId(UICanvasGroup group) =>
            group switch
            {
                UICanvasGroup.Main => UICanvasGroups.Main,
                UICanvasGroup.Floating => UICanvasGroups.Floating,
                UICanvasGroup.System => UICanvasGroups.System,
                _ => UICanvasGroups.Floating
            };

        public static UICanvasGroup IdToEnum(string groupId) =>
            groupId switch
            {
                UICanvasGroups.Main => UICanvasGroup.Main,
                UICanvasGroups.Floating => UICanvasGroup.Floating,
                UICanvasGroups.System => UICanvasGroup.System,
                _ => UICanvasGroup.Floating
            };
    }
}
