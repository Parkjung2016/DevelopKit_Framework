using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    [CustomEditor(typeof(UIToastView))]
    public sealed class UIToastViewEditor : UIViewBaseEditor
    {
        private const float CompactFieldMinWidth = 105f;

        protected override VisualElement BuildDerivedFieldsSection()
        {
            var root = new VisualElement();
            root.Add(BuildItemSection());
            root.Add(BuildDisplaySection());
            root.Add(BuildTimingSection());
            root.Add(BuildStyleSection());
            return root;
        }

        private VisualElement BuildItemSection()
        {
            var section = UISystemEditorUI.BuildFieldGroup("아이템");
            PropertyField containerField = AddField(section, "container", "표시 위치");
            containerField.tooltip = "비워 두면 Toast View의 RectTransform을 사용합니다.";

            SerializedProperty itemPrefab = serializedObject.FindProperty("itemPrefab");
            AddField(section, itemPrefab, "아이템 프리팹");

            var missingPrefab = UISystemEditorUI.BuildHelpBox(
                "토스트를 표시하려면 UIToastItem 프리팹을 연결해야 합니다.",
                HelpBoxMessageType.Warning);
            missingPrefab.style.marginTop = 4;
            section.Add(missingPrefab);

            void RefreshPrefabWarning() =>
                missingPrefab.style.display = itemPrefab.objectReferenceValue == null
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            section.TrackPropertyValue(itemPrefab, _ => RefreshPrefabWarning());
            RefreshPrefabWarning();
            return section;
        }

        private VisualElement BuildDisplaySection()
        {
            var section = UISystemEditorUI.BuildFieldGroup("표시");
            SerializedProperty displayMode = serializedObject.FindProperty("displayMode");
            SerializedProperty maxVisible = serializedObject.FindProperty("maxVisible");

            var modeLabel = new Label("표시 방식");
            modeLabel.style.fontSize = 10;
            modeLabel.style.color = new Color(0.72f, 0.72f, 0.72f);
            modeLabel.style.marginBottom = 3;
            section.Add(modeLabel);

            var modeBar = new VisualElement();
            modeBar.style.flexDirection = FlexDirection.Row;
            modeBar.style.marginBottom = 5;
            section.Add(modeBar);

            Button queueButton = null;
            Button stackButton = null;
            Button replaceButton = null;
            PropertyField maxVisibleField = null;
            Label modeHelp = null;

            queueButton = CreateModeButton("순차", ToastDisplayMode.Queue);
            stackButton = CreateModeButton("쌓기", ToastDisplayMode.Stack);
            replaceButton = CreateModeButton("교체", ToastDisplayMode.Replace);
            modeBar.Add(queueButton);
            modeBar.Add(stackButton);
            modeBar.Add(replaceButton);

            maxVisibleField = AddField(section, maxVisible, "최대 표시 개수");
            AddField(section, "suppressDuplicates", "같은 메시지 합치기");

            modeHelp = UISystemEditorUI.BuildHint(string.Empty);
            modeHelp.style.marginTop = 3;
            section.Add(modeHelp);

            Button CreateModeButton(string label, ToastDisplayMode mode)
            {
                var button = new Button(() => SetMode(mode)) { text = label };
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                button.style.height = 24;
                button.style.marginRight = mode == ToastDisplayMode.Replace ? 0 : 3;
                return button;
            }

            void SetMode(ToastDisplayMode mode)
            {
                if (displayMode.enumValueIndex == (int)mode)
                    return;

                displayMode.enumValueIndex = (int)mode;
                serializedObject.ApplyModifiedProperties();
                RefreshMode();
            }

            void RefreshMode()
            {
                var mode = (ToastDisplayMode)displayMode.enumValueIndex;
                UISystemEditorUI.ApplySubTabStyle(queueButton, mode == ToastDisplayMode.Queue);
                UISystemEditorUI.ApplySubTabStyle(stackButton, mode == ToastDisplayMode.Stack);
                UISystemEditorUI.ApplySubTabStyle(replaceButton, mode == ToastDisplayMode.Replace);

                maxVisibleField.style.display = mode == ToastDisplayMode.Stack
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                modeHelp.text = mode switch
                {
                    ToastDisplayMode.Queue => "메시지를 한 번에 하나씩 순서대로 표시합니다.",
                    ToastDisplayMode.Stack => "여러 메시지를 설정한 개수까지 함께 표시합니다.",
                    ToastDisplayMode.Replace => "새 메시지가 현재 메시지를 바로 교체합니다.",
                    _ => string.Empty
                };
            }

            section.TrackPropertyValue(displayMode, _ => RefreshMode());
            RefreshMode();
            return section;
        }

        private VisualElement BuildTimingSection()
        {
            var section = UISystemEditorUI.BuildFieldGroup("시간 (초)");
            VisualElement timeBar = BuildCompactRow(
                CreateDurationField("defaultDuration", "표시", 0.01f),
                CreateDurationField("fadeInDuration", "나타남", 0f),
                CreateDurationField("fadeOutDuration", "사라짐", 0f));
            section.Add(timeBar);
            return section;
        }

        private VisualElement BuildStyleSection()
        {
            var section = UISystemEditorUI.BuildFieldGroup("스타일");
            VisualElement colorBar = BuildCompactRow(
                CreateColorField("defaultBackgroundColor", "배경"),
                CreateColorField("defaultTextColor", "글자"));
            section.Add(colorBar);
            AddField(section, "styles", "타입별 스타일");
            return section;
        }

        private FloatField CreateDurationField(string propertyName, string label, float minimum)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            var field = new FloatField(label) { isDelayed = true };
            PrepareCompactField(field);
            field.BindProperty(property);
            field.RegisterValueChangedCallback(evt =>
            {
                float clamped = Mathf.Max(minimum, evt.newValue);
                if (Mathf.Approximately(clamped, evt.newValue))
                    return;

                property.floatValue = clamped;
                serializedObject.ApplyModifiedProperties();
                field.SetValueWithoutNotify(clamped);
            });
            return field;
        }

        private ColorField CreateColorField(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            var field = new ColorField(label);
            PrepareCompactField(field);
            field.BindProperty(property);
            return field;
        }

        private static VisualElement BuildCompactRow(params VisualElement[] fields)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginLeft = -3;
            row.style.marginRight = -3;

            for (int i = 0; i < fields.Length; i++)
            {
                fields[i].style.flexGrow = 1;
                fields[i].style.flexBasis = 0;
                fields[i].style.minWidth = CompactFieldMinWidth;
                fields[i].style.marginLeft = 3;
                fields[i].style.marginRight = 3;
                row.Add(fields[i]);
            }

            return row;
        }

        private static void PrepareCompactField<TValue>(BaseField<TValue> field)
        {
            field.labelElement.style.minWidth = 42;
            field.labelElement.style.width = StyleKeyword.Auto;
            field.labelElement.style.marginRight = 4;
        }

        private PropertyField AddField(VisualElement parent, string propertyName, string label) =>
            AddField(parent, serializedObject.FindProperty(propertyName), label);

        private PropertyField AddField(VisualElement parent, SerializedProperty property, string label)
        {
            var field = new PropertyField(property.Copy(), label);
            field.Bind(serializedObject);
            parent.Add(field);
            return field;
        }
    }
}
