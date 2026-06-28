using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal sealed class LayerOptionList
    {
        public List<string> Values { get; } = new();
        public List<string> Labels { get; } = new();
    }

    internal static class UISystemEditorLayers
    {
        public static event Action LayerIdChanged;

        public static void NotifyLayerIdChanged() => LayerIdChanged?.Invoke();
        public static LayerOptionList BuildOptions(string defaultLayer, string currentLayerId, UILayerSettings settings = null)
        {
            var options = new LayerOptionList();
            options.Values.Add(string.Empty);
            options.Labels.Add($"기본값 ({defaultLayer})");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            settings ??= UISystemEditorAssets.LoadOrFindLayerSettings();

            if (settings != null)
            {
                IReadOnlyList<UILayerDefinition> layers = settings.Layers;
                for (int i = 0; i < layers.Count; i++)
                {
                    UILayerDefinition layer = layers[i];
                    if (layer == null || string.IsNullOrEmpty(layer.LayerId))
                        continue;

                    if (!seen.Add(layer.LayerId))
                        continue;

                    options.Values.Add(layer.LayerId);
                    options.Labels.Add(FormatLayerLabel(layer));
                }
            }
            else
            {
                IReadOnlyList<string> builtIn = UISystemBuiltIn.LayerIds;
                for (int i = 0; i < builtIn.Count; i++)
                {
                    string layerId = builtIn[i];
                    if (!seen.Add(layerId))
                        continue;

                    options.Values.Add(layerId);
                    options.Labels.Add(layerId);
                }
            }

            if (!string.IsNullOrEmpty(currentLayerId) && !options.Values.Contains(currentLayerId))
            {
                options.Values.Add(currentLayerId);
                options.Labels.Add($"{currentLayerId} (현재 값)");
            }

            return options;
        }

        public static int GetSelectedIndex(LayerOptionList options, string layerId)
        {
            for (int i = 0; i < options.Values.Count; i++)
            {
                if (options.Values[i] == layerId)
                    return i;
            }

            return 0;
        }

        public static PopupField<string> CreateLayerPopupField(
            SerializedProperty layerIdProp,
            UIViewBase view,
            UILayerSettings settings = null,
            string label = "레이어 ID")
        {
            string defaultLayer = view != null ? view.DefaultLayerId : UILayers.Popup;
            string currentLayerId = layerIdProp.stringValue;
            LayerOptionList options = BuildOptions(defaultLayer, currentLayerId, settings);
            int selectedIndex = GetSelectedIndex(options, currentLayerId);

            var popup = new PopupField<string>(
                options.Values,
                selectedIndex,
                id => FormatLayerOptionLabel(id, defaultLayer, settings),
                id => FormatLayerOptionLabel(id, defaultLayer, settings))
            {
                label = label
            };
            return popup;
        }

        public static void SyncLayerPopupField(
            PopupField<string> popup,
            SerializedProperty layerIdProp,
            UIViewBase view,
            UILayerSettings settings = null)
        {
            string defaultLayer = view != null ? view.DefaultLayerId : UILayers.Popup;
            string currentLayerId = layerIdProp.stringValue;
            LayerOptionList options = BuildOptions(defaultLayer, currentLayerId, settings);
            int selectedIndex = GetSelectedIndex(options, currentLayerId);

            popup.choices = options.Values;
            popup.SetValueWithoutNotify(options.Values[selectedIndex]);
        }

        /// <summary>레이어 ID 드롭다운을 그립니다. 값이 바뀌면 true를 반환합니다.</summary>
        public static bool DrawLayerIdPopup(SerializedProperty layerIdProp, UIViewBase view, GUIContent label = null)
        {
            if (layerIdProp == null)
                return false;

            label ??= new GUIContent("레이어 ID");
            string defaultLayer = view != null ? view.DefaultLayerId : UILayers.Popup;
            string currentLayerId = layerIdProp.stringValue;
            LayerOptionList options = BuildOptions(defaultLayer, currentLayerId);
            int selectedIndex = GetSelectedIndex(options, currentLayerId);
            string[] labels = options.Labels.ToArray();

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            if (!EditorGUI.EndChangeCheck() || newIndex < 0 || newIndex >= options.Values.Count)
                return false;

            string newLayerId = options.Values[newIndex];
            if (layerIdProp.stringValue == newLayerId)
                return false;

            Undo.RecordObject(layerIdProp.serializedObject.targetObject, "Change UI Layer");
            layerIdProp.stringValue = newLayerId;
            PersistLayerIdProperty(layerIdProp);
            NotifyLayerIdChanged();
            return true;
        }

        /// <summary>PropertyDrawer용 레이어 ID 드롭다운입니다.</summary>
        public static void DrawLayerIdPopup(Rect position, SerializedProperty layerIdProp, UIViewBase view, GUIContent label)
        {
            if (layerIdProp == null)
                return;

            label = EditorGUI.BeginProperty(position, label, layerIdProp);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            string defaultLayer = view != null ? view.DefaultLayerId : UILayers.Popup;
            string currentLayerId = layerIdProp.stringValue;
            LayerOptionList options = BuildOptions(defaultLayer, currentLayerId);
            int selectedIndex = GetSelectedIndex(options, currentLayerId);
            string[] labels = options.Labels.ToArray();

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, selectedIndex, labels);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < options.Values.Count)
            {
                string newLayerId = options.Values[newIndex];
                if (layerIdProp.stringValue != newLayerId)
                {
                    Undo.RecordObject(layerIdProp.serializedObject.targetObject, "Change UI Layer");
                    layerIdProp.stringValue = newLayerId;
                    PersistLayerIdProperty(layerIdProp);
                    NotifyLayerIdChanged();
                }
            }

            EditorGUI.EndProperty();
        }

        public static void PersistLayerIdProperty(SerializedProperty property)
        {
            SerializedObject serializedObject = property.serializedObject;
            serializedObject.ApplyModifiedProperties();

            UnityEngine.Object targetObject = serializedObject.targetObject;
            EditorUtility.SetDirty(targetObject);

            if (targetObject is not Component component)
                return;

            GameObject gameObject = component.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            else if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                PrefabUtility.SavePrefabAsset(gameObject);
        }

        public static string GetSerializedLayerId(UIViewBase view)
        {
            if (view == null)
                return string.Empty;

            SerializedObject serializedObject = new(view);
            SerializedProperty layerIdProp = serializedObject.FindProperty("layerId");
            return layerIdProp != null ? layerIdProp.stringValue : string.Empty;
        }

        public static string FormatLayerIdLabel(UIViewBase view)
        {
            if (view == null)
                return "-";

            string serializedLayerId = GetSerializedLayerId(view);
            return string.IsNullOrEmpty(serializedLayerId)
                ? $"기본값 ({view.DefaultLayerId})"
                : serializedLayerId;
        }

        public static string BuildLayerHintText(UIViewBase view, UILayerSettings settings = null)
        {
            if (view == null)
                return string.Empty;

            string defaultLayer = view.DefaultLayerId;
            string serializedLayerId = GetSerializedLayerId(view);
            string resolvedLayerId = string.IsNullOrEmpty(serializedLayerId) ? defaultLayer : serializedLayerId;
            settings ??= UISystemEditorAssets.LoadOrFindLayerSettings();

            if (string.IsNullOrEmpty(serializedLayerId))
                return $"비어 있으면 기본값 {defaultLayer}을(를) 씁니다.";

            if (settings == null)
                return $"현재 적용 레이어: {resolvedLayerId}";

            IReadOnlyList<UILayerDefinition> layers = settings.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                UILayerDefinition definition = layers[i];
                if (definition != null && definition.LayerId == resolvedLayerId)
                {
                    string canvas = UISystemEditorCanvasGroups.FormatGroupLabel(definition.CanvasGroupId, settings);
                    if (!string.IsNullOrEmpty(definition.Description))
                        return $"{definition.Description} · Canvas {canvas}";

                    return $"현재 적용 레이어: {resolvedLayerId} · Canvas {canvas}";
                }
            }

            return $"현재 적용 레이어: {resolvedLayerId} (UILayerSettings: {settings.name})";
        }

        private static string FormatLayerLabel(UILayerDefinition layer)
        {
            string displayName = layer.DisplayName;
            return string.Equals(displayName, layer.LayerId, StringComparison.Ordinal)
                ? layer.LayerId
                : $"{layer.LayerId} · {displayName}";
        }

        private static string FormatLayerOptionLabel(string layerId, string defaultLayer, UILayerSettings settings)
        {
            if (string.IsNullOrEmpty(layerId))
                return $"기본값 ({defaultLayer})";

            settings ??= UISystemEditorAssets.LoadOrFindLayerSettings();
            if (settings != null)
            {
                IReadOnlyList<UILayerDefinition> layers = settings.Layers;
                for (int i = 0; i < layers.Count; i++)
                {
                    UILayerDefinition layer = layers[i];
                    if (layer != null && layer.LayerId == layerId)
                        return FormatLayerLabel(layer);
                }
            }

            return layerId;
        }
    }
}
