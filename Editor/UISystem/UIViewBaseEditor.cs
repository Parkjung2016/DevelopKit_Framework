using System;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    [CustomEditor(typeof(UIViewBase), true)]
    public class UIViewBaseEditor : Editor
    {
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
                ("UI 설정", () => UISystemEditorUI.OpenSettingsFor(
                    UISystemEditorAssets.LoadOrFindLayerSettings()))));
            root.Add(BuildViewSettingsSection(view));
            root.Add(BuildAutoSetupSection(view));

            VisualElement derivedFields = BuildDerivedFieldsSection();
            if (derivedFields != null)
                root.Add(derivedFields);

            AddCustomSections(root);

            root.Add(UISystemEditorUI.BuildSection("동작"));
            root.Add(BuildBehaviorSection());
            return root;
        }

        /// <summary>
        /// 파생 View 전용 에디터에서 별도 섹션을 추가할 때 사용합니다.
        /// 일반적인 SerializeField는 자동으로 표시되므로 직접 추가할 필요가 없습니다.
        /// </summary>
        protected virtual void AddCustomSections(VisualElement root)
        {
        }

        private void OnLayerIdChanged() => Repaint();

        private VisualElement BuildViewSettingsSection(UIViewBase view)
        {
            var section = UISystemEditorUI.BuildFieldGroup("뷰 설정");

            section.Add(UISystemEditorUI.BuildInfoRow(
                "viewId",
                view.gameObject.name,
                "GameObject 이름을 기본 viewId로 사용합니다."));

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
                ("canvasGroup", "자동", "루트에 없으면 CanvasGroup을 자동으로 추가합니다.")
            };

            if (view is UIPopupBase)
                rows.Add(("dimmer", "자동", "Dimmer 자식이 없으면 자동으로 만듭니다."));

            return UISystemEditorUI.BuildAutoConfigPanel("자동 구성", rows.ToArray());
        }

        protected virtual VisualElement BuildDerivedFieldsSection()
        {
            var section = UISystemEditorUI.BuildFieldGroup(
                $"{ObjectNames.NicifyVariableName(target.GetType().Name)} 설정");
            int fieldCount = 0;

            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (IsBuiltInProperty(property.propertyPath))
                    continue;

                var field = new PropertyField(property.Copy());
                field.Bind(serializedObject);
                section.Add(field);
                fieldCount++;
            }

            return fieldCount > 0 ? section : null;
        }

        private static bool IsBuiltInProperty(string propertyPath) =>
            propertyPath is "m_Script" or "layerId" or "priority" or "backBehavior";

        private VisualElement BuildBehaviorSection()
        {
            var section = new VisualElement();

            SerializedProperty priority = serializedObject.FindProperty("priority");
            SerializedProperty backBehavior = serializedObject.FindProperty("backBehavior");

            var priorityField = new PropertyField(priority.Copy(), "priority");
            priorityField.Bind(serializedObject);
            section.Add(priorityField);

            int selectedIndex = UISystemEditorViewPrefabFields.GetBackBehaviorIndex(
                (UIViewBackBehavior)backBehavior.enumValueIndex);
            var backField = new PopupField<string>(
                "backBehavior",
                new System.Collections.Generic.List<string>(UISystemEditorViewPrefabFields.BackBehaviorLabels),
                selectedIndex);
            backField.style.marginTop = 4;
            section.Add(backField);

            var backHelp = UISystemEditorUI.BuildHelpBox(
                UISystemEditorViewPrefabFields.DescribeBackBehavior(
                    (UIViewBackBehavior)backBehavior.enumValueIndex),
                HelpBoxMessageType.Info);
            backHelp.style.marginTop = 6;
            section.Add(backHelp);

            backField.RegisterValueChangedCallback(evt =>
            {
                int index = Array.IndexOf(UISystemEditorViewPrefabFields.BackBehaviorLabels, evt.newValue);
                if (index < 0)
                    return;

                Undo.RecordObject(serializedObject.targetObject, "Change UI Back Behavior");
                backBehavior.enumValueIndex = (int)UISystemEditorViewPrefabFields.BackBehaviorValues[index];
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(serializedObject.targetObject);

                if (backHelp is HelpBox box)
                {
                    box.text = UISystemEditorViewPrefabFields.DescribeBackBehavior(
                        UISystemEditorViewPrefabFields.BackBehaviorValues[index]);
                }
            });

            return section;
        }
    }
}
