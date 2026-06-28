using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>싱글톤·중복 UI 인스턴스 캐시를 관리합니다.</summary>
    internal static class UIViewInstanceCache
    {
        public static bool TryGetAlive(
            Dictionary<string, UIViewBase> instancesById,
            string viewId,
            out UIViewBase instance)
        {
            instance = null;
            if (!instancesById.TryGetValue(viewId, out UIViewBase cached))
                return false;

            if (cached != null)
            {
                instance = cached;
                return true;
            }

            instancesById.Remove(viewId);
            return false;
        }

        public static bool TryGetIdleDuplicate(
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            string viewId,
            out UIViewBase instance)
        {
            instance = null;
            if (!duplicatePoolsByViewId.TryGetValue(viewId, out List<UIViewBase> pool) || pool.Count == 0)
                return false;

            for (int i = pool.Count - 1; i >= 0; i--)
            {
                UIViewBase candidate = pool[i];
                if (candidate == null)
                {
                    pool.RemoveAt(i);
                    continue;
                }

                if (candidate.IsVisible)
                    continue;

                instance = candidate;
                return true;
            }

            if (pool.Count == 0)
                duplicatePoolsByViewId.Remove(viewId);

            return false;
        }

        public static void RegisterDuplicate(
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            string viewId,
            UIViewBase instance)
        {
            if (string.IsNullOrEmpty(viewId) || instance == null)
                return;

            if (!duplicatePoolsByViewId.TryGetValue(viewId, out List<UIViewBase> pool))
            {
                pool = new List<UIViewBase>();
                duplicatePoolsByViewId[viewId] = pool;
            }

            if (!pool.Contains(instance))
                pool.Add(instance);
        }

        public static void RemoveDuplicate(
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            string viewId,
            UIViewBase instance)
        {
            if (string.IsNullOrEmpty(viewId) || instance == null)
                return;

            if (!duplicatePoolsByViewId.TryGetValue(viewId, out List<UIViewBase> pool))
                return;

            pool.Remove(instance);
            if (pool.Count == 0)
                duplicatePoolsByViewId.Remove(viewId);
        }

        public static void PurgeStale(
            Dictionary<string, UIViewBase> instancesById,
            List<string> staleKeys)
        {
            staleKeys.Clear();
            foreach (KeyValuePair<string, UIViewBase> pair in instancesById)
            {
                if (pair.Value == null)
                    staleKeys.Add(pair.Key);
            }

            for (int i = 0; i < staleKeys.Count; i++)
                instancesById.Remove(staleKeys[i]);
        }

        public static void PurgeStaleDuplicates(
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            List<string> staleKeys)
        {
            staleKeys.Clear();
            foreach (KeyValuePair<string, List<UIViewBase>> pair in duplicatePoolsByViewId)
            {
                List<UIViewBase> pool = pair.Value;
                for (int i = pool.Count - 1; i >= 0; i--)
                {
                    if (pool[i] == null)
                        pool.RemoveAt(i);
                }

                if (pool.Count == 0)
                    staleKeys.Add(pair.Key);
            }

            for (int i = 0; i < staleKeys.Count; i++)
                duplicatePoolsByViewId.Remove(staleKeys[i]);
        }

        public static void CollectAllTracked(
            Dictionary<string, UIViewBase> instancesById,
            Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId,
            List<UIViewBase> buffer)
        {
            buffer.Clear();

            foreach (UIViewBase instance in instancesById.Values)
            {
                if (instance != null && !buffer.Contains(instance))
                    buffer.Add(instance);
            }

            foreach (KeyValuePair<string, List<UIViewBase>> pair in duplicatePoolsByViewId)
            {
                List<UIViewBase> pool = pair.Value;
                for (int i = 0; i < pool.Count; i++)
                {
                    UIViewBase instance = pool[i];
                    if (instance != null && !buffer.Contains(instance))
                        buffer.Add(instance);
                }
            }
        }

        public static void ClearDuplicates(Dictionary<string, List<UIViewBase>> duplicatePoolsByViewId) =>
            duplicatePoolsByViewId.Clear();
    }
}
