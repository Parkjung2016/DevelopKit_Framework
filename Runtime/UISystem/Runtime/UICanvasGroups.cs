using System;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>기본 Canvas 묶음 ID입니다. 프로젝트에서 추가 ID를 정의할 수 있습니다.</summary>
    public static class UICanvasGroups
    {
        public const string Main = "Main";
        public const string Floating = "Floating";
        public const string System = "System";

        public static bool IsBuiltIn(string groupId) =>
            string.Equals(groupId, Main, StringComparison.Ordinal)
            || string.Equals(groupId, Floating, StringComparison.Ordinal)
            || string.Equals(groupId, System, StringComparison.Ordinal);
    }
}
