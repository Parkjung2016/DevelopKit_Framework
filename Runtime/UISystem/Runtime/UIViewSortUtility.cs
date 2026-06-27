using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>Back 입력 대상 뷰를 레이어·우선순위·표시 순서로 정렬합니다.</summary>
    internal static class UIViewSortUtility
    {
        public static void SortByBackPriority(
            List<IUIView> views,
            IReadOnlyList<IUIView> showOrder,
            UILayerRegistry registry)
        {
            views.Sort((left, right) => CompareForBack(left, right, showOrder, registry));
        }

        public static int CompareForBack(
            IUIView left,
            IUIView right,
            IReadOnlyList<IUIView> showOrder,
            UILayerRegistry registry)
        {
            int leftOrder = registry != null ? registry.GetSortOrder(left.LayerId) : 0;
            int rightOrder = registry != null ? registry.GetSortOrder(right.LayerId) : 0;
            int layerCompare = rightOrder.CompareTo(leftOrder);
            if (layerCompare != 0)
                return layerCompare;

            int priorityCompare = right.Priority.CompareTo(left.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            return GetShowIndex(right, showOrder).CompareTo(GetShowIndex(left, showOrder));
        }

        private static int GetShowIndex(IUIView view, IReadOnlyList<IUIView> showOrder)
        {
            for (int i = showOrder.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(showOrder[i], view))
                    return i;
            }

            return -1;
        }
    }
}
