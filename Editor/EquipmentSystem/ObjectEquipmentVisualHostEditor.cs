using System;
using System.Collections.Generic;
using System.Linq;
using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.EquipmentSystem
{
    [CustomEditor(typeof(ObjectEquipmentVisualHost))]
    internal sealed class ObjectEquipmentVisualHostEditor : UnityEditor.Editor
    {
        private Label socketSummaryLabel;
        private EquipmentVisualSlotBindingView bindingView;
        private ObjectEquipmentVisualHost host;

        public override VisualElement CreateInspectorGUI()
        {
            host = (ObjectEquipmentVisualHost)target;

            var root = new VisualElement();
            InventoryEditorStyleSheet.Apply(root);
            serializedObject.Update();

            root.Add(new PropertyField(serializedObject.FindProperty("socketSystem")));
            root.Add(new PropertyField(serializedObject.FindProperty("slotLayoutGuide")));
            root.Add(BuildSocketsSection());
            root.Add(BuildBindingsSection());

            RebuildBindings();
            return root;
        }

        private VisualElement BuildSocketsSection()
        {
            var section = InventoryEditorUIFactory.CreateSection("Object Sockets");
            section.style.marginTop = 8;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 6;

            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Refresh", RebuildBindings));

            socketSummaryLabel = new Label { style = { flexGrow = 1, marginLeft = 8, fontSize = 11 } };
            toolbar.Add(socketSummaryLabel);
            section.Add(toolbar);

            return section;
        }

        private VisualElement BuildBindingsSection()
        {
            bindingView = new EquipmentVisualSlotBindingView(serializedObject, RebuildBindings);
            var section = bindingView.BuildRoot();

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginBottom = 6;

            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Add Row", () =>
            {
                var bindingsProp = serializedObject.FindProperty("slotSocketBindings");
                int newSlotIndex = bindingsProp.arraySize;

                bindingsProp.InsertArrayElementAtIndex(bindingsProp.arraySize);
                var element = bindingsProp.GetArrayElementAtIndex(bindingsProp.arraySize - 1);
                element.FindPropertyRelative("EquipSlotIndex").intValue = newSlotIndex;
                element.FindPropertyRelative("SocketKey").stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
                RebuildBindings();
            }));

            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Match Slot Count", () =>
            {
                var guide = serializedObject.FindProperty("slotLayoutGuide").objectReferenceValue as EquipmentSetupSO;
                if (guide == null)
                {
                    EditorUtility.DisplayDialog("Slot Layout Guide", "Slot Layout Guide에 EquipmentSetupSO를 지정하세요.", "OK");
                    return;
                }

                guide.Normalize();
                var bindingsProp = serializedObject.FindProperty("slotSocketBindings");
                bindingsProp.arraySize = guide.SlotCount;

                for (int i = 0; i < bindingsProp.arraySize; i++)
                {
                    var element = bindingsProp.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("EquipSlotIndex").intValue = i;
                    if (string.IsNullOrEmpty(element.FindPropertyRelative("SocketKey").stringValue))
                        element.FindPropertyRelative("SocketKey").stringValue = string.Empty;
                }

                serializedObject.ApplyModifiedProperties();
                RebuildBindings();
            }));

            section.Insert(1, toolbar);
            return section;
        }

        private void RebuildBindings()
        {
            if (host == null || bindingView == null)
                return;

            serializedObject.Update();

            string[] socketKeys = CollectSocketKeys(host);
            UpdateSocketSummary(socketKeys);

            var guide = serializedObject.FindProperty("slotLayoutGuide").objectReferenceValue as EquipmentSetupSO;
            bindingView.Rebuild(socketKeys, guide);
        }

        private void UpdateSocketSummary(IReadOnlyList<string> socketKeys)
        {
            if (socketSummaryLabel == null)
                return;

            socketSummaryLabel.text = socketKeys.Count == 0
                ? "자식 ObjectSocket 없음"
                : $"{socketKeys.Count}개: {string.Join(", ", socketKeys)}";
        }

        private static string[] CollectSocketKeys(ObjectEquipmentVisualHost host)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var keys = new List<string>();
            ObjectSocket[] sockets = host.GetComponentsInChildren<ObjectSocket>(true);

            foreach (ObjectSocket socket in sockets.OrderBy(s => s.name, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(socket.name))
                    continue;

                if (seen.Add(socket.name))
                    keys.Add(socket.name);
            }

            return keys.ToArray();
        }
    }
}
