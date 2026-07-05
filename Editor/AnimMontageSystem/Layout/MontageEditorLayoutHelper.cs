using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageEditorLayoutHelper
    {
        public static VisualElement CreatePanelHeader(string title)
        {
            var header = new VisualElement();
            header.AddToClassList(AnimMontageEditorStyles.PanelHeaderClass);
            header.style.flexShrink = 0;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList(AnimMontageEditorStyles.PanelHeaderTitleClass);
            header.Add(titleLabel);
            return header;
        }

        public static void ConfigureSplit(TwoPaneSplitView split, string viewDataKey = null)
        {
            split.style.flexGrow = 1;
            split.style.flexShrink = 1;
            split.style.minWidth = 0;
            split.style.minHeight = 0;

            if (!string.IsNullOrEmpty(viewDataKey))
                split.viewDataKey = viewDataKey;

            split.contentContainer.style.flexGrow = 1;
            split.contentContainer.style.flexShrink = 1;
            split.contentContainer.style.minWidth = 0;
            split.contentContainer.style.minHeight = 0;
            split.contentContainer.style.overflow = Overflow.Hidden;
        }

        public static void ConfigurePane(VisualElement pane)
        {
            pane.style.flexGrow = 1;
            pane.style.flexShrink = 1;
            pane.style.minWidth = 0;
            pane.style.minHeight = 0;
            pane.style.overflow = Overflow.Hidden;
        }
    }
}
