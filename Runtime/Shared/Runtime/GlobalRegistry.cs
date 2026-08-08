using System;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.Shared.Runtime
{
    /// <summary>런타임에서 공유하는 서비스나 카탈로그의 현재 인스턴스를 보관합니다.</summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class GlobalRegistry<T> where T : class
    {
        private static T current;

        public static bool IsReady => current != null;
        public static T Current => current;

        public static void Set(T instance)
        {
            current = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public static void Clear() => current = null;
        public static T Resolve(T instance = null) => instance ?? current;
        public static T ResolveOrDefault(T instance, T fallback) => instance ?? current ?? fallback;
    }
}