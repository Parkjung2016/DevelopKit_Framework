using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>Back 입력을 레이어·우선순위 규칙에 따라 라우팅합니다.</summary>
    internal static class UIBackDispatcher
    {
        public static bool TryHandleBack(
            UINavigationStack screenStack,
            IReadOnlyList<IUIView> floatingViews,
            UILayerRegistry registry,
            List<IUIView> candidates = null)
        {
            if (TryHandleFloatingViews(floatingViews, registry, candidates))
                return true;

            UIScreenBase topScreen = screenStack.Peek;
            if (topScreen != null && topScreen.IsVisible)
                return topScreen.HandleBack();

            return false;
        }

        private static bool TryHandleFloatingViews(
            IReadOnlyList<IUIView> floatingViews,
            UILayerRegistry registry,
            List<IUIView> candidates)
        {
            if (floatingViews == null || floatingViews.Count == 0)
                return false;

            candidates ??= new List<IUIView>();
            candidates.Clear();
            for (int i = 0; i < floatingViews.Count; i++)
            {
                IUIView view = floatingViews[i];
                if (view == null || !view.IsVisible)
                    continue;

                if (!view.BlocksBack && !view.CloseOnBack)
                    continue;

                candidates.Add(view);
            }

            if (candidates.Count == 0)
                return false;

            UIViewSortUtility.SortByBackPriority(candidates, floatingViews, registry);

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].HandleBack())
                    return true;
            }

            return false;
        }
    }
}
