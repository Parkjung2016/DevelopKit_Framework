using System;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal static class UISystemEditorViewPrefabFields
    {
        public static readonly string[] BackBehaviorLabels =
        {
            "Back으로 닫기",
            "직접 처리",
            "Back 제외"
        };

        public static readonly UIViewBackBehavior[] BackBehaviorValues =
        {
            UIViewBackBehavior.CloseOnBack,
            UIViewBackBehavior.HandleManually,
            UIViewBackBehavior.PassThrough
        };

        public static VisualElement BuildSection(UIViewBase prefab, Action onChanged)
        {
            var section = UISystemEditorUI.BuildFieldGroup("프리팹 설정");
            if (prefab == null)
            {
                section.Add(UISystemEditorUI.BuildHint("프리팹을 지정하면 layerId · priority · backBehavior를 여기서 편집할 수 있습니다."));
                return section;
            }

            var prefabObject = new SerializedObject(prefab);
            SerializedProperty layerIdProp = prefabObject.FindProperty("layerId");
            SerializedProperty priorityProp = prefabObject.FindProperty("priority");
            SerializedProperty backBehaviorProp = prefabObject.FindProperty("backBehavior");

            var layerHint = UISystemEditorUI.BuildHint(UISystemEditorLayers.BuildLayerHintText(prefab));
            layerHint.name = "layer-hint";

            if (layerIdProp != null)
            {
                PopupField<string> layerPopup = UISystemEditorLayers.CreateLayerPopupField(layerIdProp, prefab);
                layerPopup.RegisterValueChangedCallback(evt =>
                {
                    string newLayerId = evt.newValue ?? string.Empty;
                    if (string.Equals(layerIdProp.stringValue, newLayerId, StringComparison.Ordinal))
                        return;

                    Undo.RecordObject(prefab, "Change UI Layer");
                    layerIdProp.stringValue = newLayerId;
                    UISystemEditorLayers.PersistLayerIdProperty(layerIdProp);
                    prefabObject.Update();
                    layerHint.text = UISystemEditorLayers.BuildLayerHintText(prefab);
                    onChanged?.Invoke();
                });
                section.Add(layerPopup);
            }

            section.Add(layerHint);

            if (priorityProp != null)
            {
                var priorityField = new PropertyField(priorityProp.Copy(), "우선순위 (priority)");
                priorityField.Bind(prefabObject);
                priorityField.RegisterValueChangeCallback(_ =>
                {
                    prefabObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);
                    onChanged?.Invoke();
                });
                section.Add(priorityField);
                section.Add(UISystemEditorUI.BuildHint("같은 레이어 안에서 큰 값일수록 앞에 그려집니다."));
            }

            if (backBehaviorProp != null)
            {
                int selectedIndex = GetBackBehaviorIndex((UIViewBackBehavior)backBehaviorProp.enumValueIndex);
                var backField = new PopupField<string>(
                    "Back 동작 (backBehavior)",
                    new System.Collections.Generic.List<string>(BackBehaviorLabels),
                    selectedIndex);
                section.Add(backField);

                var backHelp = UISystemEditorUI.BuildHelpBox(
                    DescribeBackBehavior((UIViewBackBehavior)backBehaviorProp.enumValueIndex),
                    HelpBoxMessageType.Info);
                backHelp.style.marginTop = 4;
                section.Add(backHelp);

                backField.RegisterValueChangedCallback(evt =>
                {
                    int index = Array.IndexOf(BackBehaviorLabels, evt.newValue);
                    if (index < 0)
                        return;

                    Undo.RecordObject(prefab, "Change UI Back Behavior");
                    backBehaviorProp.enumValueIndex = (int)BackBehaviorValues[index];
                    prefabObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);

                    if (backHelp is HelpBox box)
                        box.text = DescribeBackBehavior(BackBehaviorValues[index]);

                    onChanged?.Invoke();
                });
            }

            section.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged += RefreshLayerHint;
                UISystemEditorCanvasGroups.CanvasGroupsChanged += RefreshLayerHint;
            });
            section.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                UISystemEditorLayers.LayerIdChanged -= RefreshLayerHint;
                UISystemEditorCanvasGroups.CanvasGroupsChanged -= RefreshLayerHint;
            });

            void RefreshLayerHint()
            {
                prefabObject.Update();
                layerHint.text = UISystemEditorLayers.BuildLayerHintText(prefab);
            }

            return section;
        }

        public static int GetBackBehaviorIndex(UIViewBackBehavior behavior)
        {
            for (int i = 0; i < BackBehaviorValues.Length; i++)
            {
                if (BackBehaviorValues[i] == behavior)
                    return i;
            }

            return 0;
        }

        public static string DescribeBackBehavior(UIViewBackBehavior behavior) =>
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
