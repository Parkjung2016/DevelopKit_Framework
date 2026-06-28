using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>중복 허용 UI 인스턴스의 표시용 이름 접미사를 재사용합니다.</summary>
    internal static class UIViewDuplicateInstanceNaming
    {
        private static readonly Dictionary<string, int> nextIndexByViewId = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, SortedSet<int>> freeIndicesByViewId = new(StringComparer.Ordinal);

        public static int Allocate(string viewId)
        {
            if (string.IsNullOrEmpty(viewId))
                return 1;

            if (freeIndicesByViewId.TryGetValue(viewId, out SortedSet<int> freeIndices) && freeIndices.Count > 0)
            {
                int reused = freeIndices.Min;
                freeIndices.Remove(reused);
                return reused;
            }

            if (!nextIndexByViewId.TryGetValue(viewId, out int next))
                next = 1;

            nextIndexByViewId[viewId] = next + 1;
            return next;
        }

        public static void Release(string viewId, int index)
        {
            if (string.IsNullOrEmpty(viewId) || index <= 0)
                return;

            if (!freeIndicesByViewId.TryGetValue(viewId, out SortedSet<int> freeIndices))
            {
                freeIndices = new SortedSet<int>();
                freeIndicesByViewId[viewId] = freeIndices;
            }

            freeIndices.Add(index);
        }

        public static void ResetAll()
        {
            nextIndexByViewId.Clear();
            freeIndicesByViewId.Clear();
        }
    }
}
