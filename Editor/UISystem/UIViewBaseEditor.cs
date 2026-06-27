using System;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    [CustomEditor(typeof(UIViewBase), true)]
    public sealed class UIViewBaseEditor : Editor
    {
        private static readonly string[] BackBehaviorLabels =
        {
            "Back으로 닫기",
            "직접 처리",
            "Back 제외"
        };

        private static readonly UIViewBackBehavior[] BackBehaviorValues =
        {
            UIViewBackBehavior.CloseOnBack,
            UIViewBackBehavior.HandleManually,
            UIViewBackBehavior.PassThrough
        };

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var view = (UIViewBase)target;

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged += OnLayerIdChanged;
                UISystemEditorCanvasGroups.CanvasGroupsChanged += OnLayerIdChanged;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged -= OnLayerIdChanged;
                UISystemEditorCanvasGroups.CanvasGroupsChanged -= OnLayerIdChanged;
            });

            root.Add(UISystemEditorUI.BuildToolbar(
                ("UI 설정 창", () => UISystemEditorUI.OpenSettingsFor(UISystemEditorAssets.LoadOrFindLayerSettings()))));
            root.Add(BuildViewSettingsSection(view));
            root.Add(BuildAutoSetupSection(view));
            root.Add(UISystemEditorUI.BuildSection("동작"));
            root.Add(BuildBehaviorSection());
            return root;
        }

        private void OnLayerIdChanged() => Repaint();

        private VisualElement BuildViewSettingsSection(UIViewBase view)
        {
            var section = UISystemEditorUI.BuildFieldGroup("뷰 설정");

            section.Add(UISystemEditorUI.BuildInfoRow(
                "viewId",
                view.gameObject.name,
                "GameObject 이름이 viewId입니다."));

            var layerField = new IMGUIContainer(DrawLayerIdField);
            layerField.style.marginTop = 4;
            layerField.style.height = EditorGUIUtility.singleLineHeight * 2 + 18;
            section.Add(layerField);

            if (UISystemEditorAssets.LoadOrFindLayerSettings() == null)
            {
                section.Add(UISystemEditorUI.BuildHelpBox(
                    "UILayerSettings 에셋이 없습니다. PJDev > UI > Settings에서 레이어를 만들어 주세요.",
                    HelpBoxMessageType.Warning));
            }

            return section;
        }

        private void DrawLayerIdField()
        {
            serializedObject.Update();

            var view = (UIViewBase)target;
            SerializedProperty layerIdProp = serializedObject.FindProperty("layerId");
            if (layerIdProp == null)
                return;

            UISystemEditorLayers.DrawLayerIdPopup(layerIdProp, view);
            EditorGUILayout.LabelField(
                UISystemEditorLayers.BuildLayerHintText(view),
                EditorStyles.wordWrappedMiniLabel);
        }

        private static VisualElement BuildAutoSetupSection(UIViewBase view)
        {
            var rows = new System.Collections.Generic.List<(string label, string value, string description)>
            {
                ("canvasGroup", "자동", "루트에 없으면 CanvasGroup을 추가합니다.")
            };

            if (view is UIPopupBase)
                rows.Add(("dimmer", "자동", "Dimmer 자식이 없으면 만듭니다."));

            return UISystemEditorUI.BuildAutoConfigPanel("자동 구성", rows.ToArray());
        }

        private VisualElement BuildBehaviorSection()
        {
            var section = new VisualElement();

            SerializedProperty priority = serializedObject.FindProperty("priority");
            SerializedProperty backBehavior = serializedObject.FindProperty("backBehavior");

            var priorityField = new PropertyField(priority.Copy(), "priority");
            priorityField.Bind(serializedObject);
            section.Add(priorityField);

            int selectedIndex = GetBackBehaviorIndex((UIViewBackBehavior)backBehavior.enumValueIndex);
            var backField = new PopupField<string>(
                "backBehavior",
                new System.Collections.Generic.List<string>(BackBehaviorLabels),
                selectedIndex);
            backField.style.marginTop = 4;
            section.Add(backField);

            var backHelp = UISystemEditorUI.BuildHelpBox(
                DescribeBackBehavior((UIViewBackBehavior)backBehavior.enumValueIndex),
                HelpBoxMessageType.Info);
            backHelp.style.marginTop = 6;
            section.Add(backHelp);

            backField.RegisterValueChangedCallback(evt =>
            {
                int index = Array.IndexOf(BackBehaviorLabels, evt.newValue);
                if (index < 0)
                    return;

                Undo.RecordObject(serializedObject.targetObject, "Change UI Back Behavior");
                backBehavior.enumValueIndex = (int)BackBehaviorValues[index];
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(serializedObject.targetObject);

                if (backHelp is HelpBox box)
                    box.text = DescribeBackBehavior(BackBehaviorValues[index]);
            });

            return section;
        }

        private static int GetBackBehaviorIndex(UIViewBackBehavior behavior)
        {
            for (int i = 0; i < BackBehaviorValues.Length; i++)
            {
                if (BackBehaviorValues[i] == behavior)
                    return i;
            }

            return 0;
        }

        private static string DescribeBackBehavior(UIViewBackBehavior behavior) =>
            behavior switch
            {
                UIViewBackBehavior.CloseOnBack =>
                    "Back 키를 누르면 이 UI가 닫힙니다. 일반 팝업·모달에 쓰면 됩니다.",
                UIViewBackBehavior.HandleManually =>
                    "Back 키는 OnBack()으로만 넘어옵니다. 직접 닫거나 막으려면 OnBack()을 오버라이드하세요.",
                UIViewBackBehavior.PassThrough =>
                    "이 UI는 Back을 받지 않습니다. 뒤에 열려 있는 UI가 Back을 처리합니다.",
                _ => string.Empty
            };
    }
}
