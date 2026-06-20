using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal abstract class InventoryEditorPanelBase
    {
        protected InventoryEditorPanelBase(InventoryEditorContext context) =>
            Context = context;

        protected InventoryEditorContext Context { get; }

        public abstract string Title { get; }

        public VisualElement Root { get; protected set; }

        public abstract void Refresh();

        public void Build(VisualElement host)
        {
            Root = host;
            InventoryEditorUiFactory.ConfigurePanelRoot(host);
            Refresh();
        }

        protected VisualElement CreateMissingSetupMessage(string hint)
        {
            var box = new HelpBox(
                "InventorySetupSO를 상단에서 선택하거나 생성하세요.\n" + hint,
                HelpBoxMessageType.Info);
            box.style.marginTop = 8;
            return box;
        }
    }
}
