using System;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.Shared.Runtime
{
    /// <summary>모듈 Catalog static class의 공통 백엔드.</summary>
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
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            current = instance;
        }

        public static void Clear() => current = null;

        public static T Resolve(T instance = null) => instance ?? current;

        public static T ResolveOrDefault(T instance, T fallback) => instance ?? current ?? fallback;
    }
}
