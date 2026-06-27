using System;
using System.Collections.Generic;
using System.Text;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal static class UISystemEditorUI
    {
        public static bool PreferWindowLayout { get; set; }

        public enum LayerSettingsSection
        {
            CanvasGroups,
            Layers
        }

        public static LayerSettingsSection LayerSettingsSectionMode { get; set; } = LayerSettingsSection.Layers;

        public static VisualElement BuildHeader(string title, string subtitle = null)
        {
            var box = new VisualElement();
            box.style.marginBottom = 10;
            box.style.paddingTop = 4;
            box.style.paddingBottom = 4;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 14;
            box.Add(titleLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = new Label(subtitle);
                sub.style.whiteSpace = WhiteSpace.Normal;
                sub.style.color = new Color(0.75f, 0.75f, 0.75f);
                sub.style.marginTop = 2;
                box.Add(sub);
            }

            return box;
        }

        public static VisualElement BuildOpenSettingsToolbar(UnityEngine.Object asset)
        {
            return BuildToolbar(("설정 창 열기", () => OpenSettingsFor(asset)));
        }

        public static void OpenSettingsFor(UnityEngine.Object asset)
        {
            EditorApplication.delayCall += () =>
            {
                if (asset is UIViewCatalog catalog)
                    UISystemSettingsWindow.OpenViewCatalog(catalog);
                else if (asset is UILayerSettings settings)
                    UISystemSettingsWindow.OpenLayerSettings(settings);
                else
                    UISystemSettingsWindow.Open();
            };
        }

        public static VisualElement BuildToolbar(params (string label, Action action)[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 8;

            for (int i = 0; i < buttons.Length; i++)
            {
                (string label, Action action) = buttons[i];
                var button = new Button(action) { text = label };
                button.style.marginRight = 4;
                button.style.marginBottom = 4;
                button.style.height = 22;
                row.Add(button);
            }

            return row;
        }

        public static VisualElement BuildHelpBox(string message, HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            var help = new HelpBox(message, type);
            help.style.marginBottom = 8;
            help.style.whiteSpace = WhiteSpace.Normal;
            return help;
        }

        public static VisualElement BuildBuiltInLayersReference(bool asFoldout = true)
        {
            var table = BuildBuiltInLayersTable();
            var help = BuildHelpBox(
                "프리팹 layerId와 UILayerSettings에 위 ID를 사용합니다.\n" +
                "코드: UISystemBuiltIn.LayerIds, UILayerSettings.BuiltIn, UILayers.*",
                HelpBoxMessageType.Info);

            if (!asFoldout)
            {
                var container = new VisualElement();
                container.Add(table);
                container.Add(help);
                return container;
            }

            var foldout = new Foldout { text = "기본 레이어 (참고)", value = false };
            foldout.style.marginBottom = 8;
            foldout.Add(table);
            foldout.Add(help);
            return foldout;
        }

        private static VisualElement BuildBuiltInLayersTable()
        {
            var table = new VisualElement();
            table.style.backgroundColor = new Color(0f, 0f, 0f, 0.15f);
            table.style.borderTopWidth = 1;
            table.style.borderBottomWidth = 1;
            table.style.borderLeftWidth = 1;
            table.style.borderRightWidth = 1;
            table.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            table.style.borderBottomColor = table.style.borderTopColor.value;
            table.style.borderLeftColor = table.style.borderTopColor.value;
            table.style.borderRightColor = table.style.borderTopColor.value;
            table.style.paddingLeft = 6;
            table.style.paddingRight = 6;
            table.style.paddingTop = 4;
            table.style.paddingBottom = 4;

            table.Add(BuildReferenceRow("레이어 ID", "순서", "Canvas 묶음", "화면 스택", true));
            IReadOnlyList<BuiltInLayerInfo> layers = UISystemBuiltIn.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                BuiltInLayerInfo layer = layers[i];
                table.Add(BuildReferenceRow(
                    layer.LayerId,
                    layer.SortOrder.ToString(),
                    GetCanvasGroupLabel(layer.CanvasGroupId),
                    layer.UseScreenStack ? "예" : "-",
                    false));
            }

            return table;
        }

        public static VisualElement BuildSection(string title)
        {
            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 6;
            label.style.fontSize = 12;
            return label;
        }

        public static Label BuildSectionLabel(string title)
        {
            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 6;
            label.style.fontSize = 12;
            return label;
        }

        public static VisualElement BuildActionPanel()
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.1f);
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderBottomColor = panel.style.borderTopColor.value;
            panel.style.borderLeftColor = panel.style.borderTopColor.value;
            panel.style.borderRightColor = panel.style.borderTopColor.value;
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            panel.style.marginBottom = 10;
            return panel;
        }

        public static VisualElement BuildDragDropZone(string label, Action<IReadOnlyList<GameObject>> onDrop)
        {
            var zone = new VisualElement();
            zone.style.height = 40;
            zone.style.marginTop = 6;
            zone.style.justifyContent = Justify.Center;
            zone.style.alignItems = Align.Center;
            zone.style.backgroundColor = new Color(0.15f, 0.35f, 0.55f, 0.12f);
            zone.style.borderTopWidth = 1;
            zone.style.borderBottomWidth = 1;
            zone.style.borderLeftWidth = 1;
            zone.style.borderRightWidth = 1;
            zone.style.borderTopColor = new Color(0.25f, 0.45f, 0.65f, 0.5f);
            zone.style.borderBottomColor = zone.style.borderTopColor.value;
            zone.style.borderLeftColor = zone.style.borderTopColor.value;
            zone.style.borderRightColor = zone.style.borderTopColor.value;
            zone.style.borderTopLeftRadius = 4;
            zone.style.borderTopRightRadius = 4;
            zone.style.borderBottomLeftRadius = 4;
            zone.style.borderBottomRightRadius = 4;

            var hint = new Label(label);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.color = new Color(0.8f, 0.85f, 0.9f);
            hint.style.fontSize = 11;
            zone.Add(hint);

            zone.RegisterCallback<DragUpdatedEvent>(_ => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            zone.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();
                var dropped = new List<GameObject>();
                for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                {
                    if (DragAndDrop.objectReferences[i] is GameObject go)
                        dropped.Add(go);
                }

                if (dropped.Count > 0)
                    onDrop(dropped);

                evt.StopPropagation();
            });

            return zone;
        }

        public static VisualElement BuildFieldGroup(string title = null)
        {
            var group = new VisualElement();
            group.style.marginTop = 4;
            group.style.paddingLeft = 6;
            group.style.paddingRight = 6;
            group.style.paddingTop = 6;
            group.style.paddingBottom = 6;
            group.style.backgroundColor = new Color(0f, 0f, 0f, 0.08f);
            group.style.borderTopLeftRadius = 3;
            group.style.borderTopRightRadius = 3;
            group.style.borderBottomLeftRadius = 3;
            group.style.borderBottomRightRadius = 3;

            if (!string.IsNullOrEmpty(title))
            {
                var label = new Label(title);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.fontSize = 11;
                label.style.marginBottom = 4;
                label.style.color = new Color(0.8f, 0.8f, 0.8f);
                group.Add(label);
            }

            return group;
        }

        public static Label BuildHint(string text)
        {
            var hint = new Label(text);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.fontSize = 10;
            hint.style.color = new Color(0.65f, 0.65f, 0.65f);
            hint.style.marginTop = 2;
            hint.style.marginBottom = 4;
            return hint;
        }

        public static VisualElement BuildInfoRow(string label, string value, string description = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 4;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.05f);

            var labelColumn = new Label(label);
            labelColumn.style.width = 88;
            labelColumn.style.flexShrink = 0;
            labelColumn.style.fontSize = 11;
            labelColumn.style.color = new Color(0.7f, 0.7f, 0.7f);
            labelColumn.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(labelColumn);

            var valueColumn = new VisualElement();
            valueColumn.style.flexGrow = 1;

            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 11;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            valueColumn.Add(valueLabel);

            if (!string.IsNullOrEmpty(description))
                valueColumn.Add(BuildHint(description));

            row.Add(valueColumn);
            return row;
        }

        public static VisualElement BuildAutoConfigPanel(string title, params (string label, string value, string description)[] rows)
        {
            var panel = BuildFieldGroup(title);
            panel.style.marginBottom = 8;

            for (int i = 0; i < rows.Length; i++)
            {
                (string label, string value, string description) = rows[i];
                panel.Add(BuildInfoRow(label, value, description));
            }

            var lastRow = panel[panel.childCount - 1];
            lastRow.style.borderBottomWidth = 0;
            lastRow.style.marginBottom = 0;
            return panel;
        }

        public static Label BuildBadge(string text, Color color)
        {
            var badge = new Label(text);
            badge.style.fontSize = 10;
            badge.style.color = color;
            badge.style.paddingLeft = 5;
            badge.style.paddingRight = 5;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.marginRight = 4;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            badge.style.borderTopLeftRadius = 3;
            badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = 3;
            badge.style.borderBottomRightRadius = 3;
            return badge;
        }

        public static PropertyField CreateLabeledField(SerializedProperty property, SerializedObject owner, string label)
        {
            var field = new PropertyField(property.Copy(), label);
            field.Bind(owner);
            return field;
        }

        public static VisualElement BuildCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);
            card.style.borderBottomColor = card.style.borderTopColor.value;
            card.style.borderLeftColor = card.style.borderTopColor.value;
            card.style.borderRightColor = card.style.borderTopColor.value;
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginBottom = 6;
            return card;
        }

        public static void BindProperty(VisualElement root, SerializedProperty property, SerializedObject owner, string label = null)
        {
            PropertyField field = string.IsNullOrEmpty(label)
                ? new PropertyField(property.Copy())
                : new PropertyField(property.Copy(), label);
            field.Bind(owner);
            root.Add(field);
        }

        public static string BuildBuiltInSummaryText()
        {
            var builder = new StringBuilder();
            IReadOnlyList<BuiltInLayerInfo> layers = UISystemBuiltIn.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                BuiltInLayerInfo layer = layers[i];
                if (i > 0)
                    builder.AppendLine();

                builder.Append(layer.LayerId)
                    .Append(" · 순서 ").Append(layer.SortOrder)
                    .Append(" · ").Append(GetCanvasGroupLabel(layer.CanvasGroupId))
                    .Append(" · ").Append(layer.Description);
            }

            return builder.ToString();
        }

        private static string GetCanvasGroupLabel(string groupId)
        {
            if (UISystemBuiltIn.TryGetCanvasGroup(groupId, out BuiltInCanvasGroupInfo info))
                return info.Description;

            return groupId;
        }

        private static VisualElement BuildReferenceRow(
            string col1,
            string col2,
            string col3,
            string col4,
            bool isHeader)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;

            if (isHeader)
                row.style.unityFontStyleAndWeight = FontStyle.Bold;

            row.Add(BuildCell(col1, 28));
            row.Add(BuildCell(col2, 12));
            row.Add(BuildCell(col3, 18));
            row.Add(BuildCell(col4, 14));
            return row;
        }

        private static Label BuildCell(string text, float widthPercent)
        {
            var label = new Label(text);
            label.style.width = new Length(widthPercent, LengthUnit.Percent);
            label.style.fontSize = 11;
            return label;
        }

        public static VisualElement BuildSubTabRow(string[] labels, int selectedIndex, Action<int> onSelected)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 8;

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var button = new Button(() => onSelected(index)) { text = labels[i] };
                button.style.height = 26;
                button.style.marginRight = 4;
                button.style.flexGrow = 1;
                ApplySubTabStyle(button, index == selectedIndex);
                row.Add(button);
            }

            return row;
        }

        public static void ApplySubTabStyle(Button button, bool selected)
        {
            if (selected)
            {
                button.style.backgroundColor = new Color(0.2f, 0.45f, 0.7f, 0.35f);
                button.style.color = new Color(0.9f, 0.95f, 1f);
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                button.style.backgroundColor = new Color(0f, 0f, 0f, 0.08f);
                button.style.color = StyleKeyword.Null;
                button.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }
    }
}
