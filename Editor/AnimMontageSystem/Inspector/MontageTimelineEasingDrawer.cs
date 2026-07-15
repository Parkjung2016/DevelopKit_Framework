using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [CustomPropertyDrawer(typeof(MontageTimelineEasing))]
    internal sealed class MontageTimelineEasingDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.marginTop = 3f;
            root.style.marginBottom = 3f;
            root.style.paddingLeft = 5f;
            root.style.paddingRight = 5f;
            root.style.paddingTop = 4f;
            root.style.paddingBottom = 4f;
            root.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
            root.style.borderTopLeftRadius = 4f;
            root.style.borderTopRightRadius = 4f;
            root.style.borderBottomLeftRadius = 4f;
            root.style.borderBottomRightRadius = 4f;

            var header = new Label(property.displayName);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 2f;
            root.Add(header);

            SerializedProperty preset = property.FindPropertyRelative("preset");
            SerializedProperty duration = property.FindPropertyRelative("duration");
            SerializedProperty customCurve = property.FindPropertyRelative("customCurve");

            var presetField = new PropertyField(preset, "Preset");
            root.Add(presetField);

            var durationField = new PropertyField(duration, "Duration");
            root.Add(durationField);

            var curveField = new PropertyField(customCurve, "Curve");
            root.Add(curveField);

            void ApplyPreset()
            {
                var selectedPreset = (MontageTimelineEasePreset)preset.enumValueIndex;
                AnimationCurve curve = MontageTimelineEasing.CreatePresetCurve(selectedPreset);
                if (curve == null)
                    return;

                customCurve.animationCurveValue = curve;
                property.serializedObject.ApplyModifiedProperties();
            }

            presetField.RegisterCallback<SerializedPropertyChangeEvent>(_ => ApplyPreset());
            return root;
        }
    }
}
