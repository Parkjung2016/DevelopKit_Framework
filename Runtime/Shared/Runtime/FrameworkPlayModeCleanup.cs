using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.Shared.Runtime
{
    /// <summary>
    /// Unity 6000.5 미만(또는 AutoStaticsCleanup 미지원) 환경에서 Play Mode 종료 시 static 정리 콜백을 모읍니다.
    /// </summary>
    public static class FrameworkPlayModeCleanup
    {
        private static readonly List<Action> CleanupActions = new();

        public static void Register(Action cleanup)
        {
            if (cleanup == null)
                throw new ArgumentNullException(nameof(cleanup));

            if (!CleanupActions.Contains(cleanup))
                CleanupActions.Add(cleanup);
        }

        public static void RunAll()
        {
            for (int i = CleanupActions.Count - 1; i >= 0; i--)
                CleanupActions[i]();
        }
    }
}
