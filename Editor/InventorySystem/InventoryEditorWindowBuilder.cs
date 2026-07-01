using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEditorWindowBuilder
    {
        public static VisualElement Build(
            out ObjectField setupField,
            out ObjectField databaseSetupField,
            out VisualElement navHost,
            out VisualElement contentHost)
        {
            var root = new VisualElement();
            root.AddToClassList("inv-root");
            root.style.flexGrow = 1;

            var toolbar = new VisualElement { name = "toolbar" };
            toolbar.AddToClassList("inv-top-toolbar");

            var fieldsHost = new VisualElement { name = "toolbar-fields" };
            fieldsHost.AddToClassList("inv-top-toolbar-fields");

            setupField = new ObjectField("Container Setup") { name = "setup-field" };
            fieldsHost.Add(setupField);

            databaseSetupField = new ObjectField("Database Setup") { name = "database-setup-field" };
            fieldsHost.Add(databaseSetupField);

            var actionsHost = new VisualElement { name = "toolbar-actions" };
            actionsHost.AddToClassList("inv-top-toolbar-actions");

            actionsHost.Add(CreateToolbarButton("create-setup-btn", "New Setup"));
            actionsHost.Add(CreateToolbarButton("create-database-setup-btn", "New DB Setup"));
            actionsHost.Add(CreateToolbarButton("create-all-btn", "Create All"));
            actionsHost.Add(CreateToolbarButton("save-btn", "Save", primary: true));
            actionsHost.Add(CreateToolbarButton("refresh-btn", "Refresh"));

            toolbar.Add(fieldsHost);
            toolbar.Add(actionsHost);
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
