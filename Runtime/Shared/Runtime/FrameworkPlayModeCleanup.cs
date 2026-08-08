using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Shared.Runtime
{
    /// <summary>도메인 재로드를 사용하지 않을 때 플레이 모드 종료 시 실행할 정리 작업을 관리합니다.</summary>
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
            {
                try
                {
                    CleanupActions[i]();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}