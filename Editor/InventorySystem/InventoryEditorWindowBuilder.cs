using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEditorWindowBuilder
    {
        public static VisualElement Build(
            out ObjectField setupField,
            out VisualElement navHost,
            out VisualElement contentHost)
        {
            var root = new VisualElement();
            root.AddToClassList("inv-root");
            root.style.flexGrow = 1;

            var toolbar = new VisualElement { name = "toolbar" };
            toolbar.AddToClassList("inv-top-toolbar");

            setupField = new ObjectField("Setup") { name = "setup-field" };
            setupField.style.flexGrow = 1;
            setupField.style.marginRight = 8;
            toolbar.Add(setupField);

            toolbar.Add(CreateToolbarButton("create-setup-btn", "New Setup"));
            toolbar.Add(CreateToolbarButton("create-all-btn", "Create All"));
            toolbar.Add(CreateToolbarButton("save-btn", "Save", primary: true));
            toolbar.Add(CreateToolbarButton("refresh-btn", "Refresh"));
            root.Add(toolbar);

            var body = new VisualElement();
            body.AddToClassList("inv-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;

            navHost = new VisualElement { name = "nav-host" };
            navHost.AddToClassList("inv-sidebar");

            contentHost = new VisualElement { name = "content-host" };
            contentHost.AddToClassList("inv-content");
            contentHost.style.flexGrow = 1;
            contentHost.style.flexDirection = FlexDirection.Column;

            body.Add(navHost);
            body.Add(contentHost);
            root.Add(body);

            return root;
        }

        private static Button CreateToolbarButton(string name, string text, bool primary = false)
        {
            var button = new Button { name = name, text = text };
            button.AddToClassList("inv-btn");
            if (primary)
                button.AddToClassList("inv-btn-primary");

            return button;
        }
    }
}
