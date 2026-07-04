using System;
using System.Collections.Generic;
using System.Linq;
using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.SocketSystem
{
    [CustomEditor(typeof(ObjectSocketSystem))]
    internal sealed class ObjectSocketSystemEditor : UnityEditor.Editor
    {
        private ObjectSocketSystem socketSystem;
        private VisualElement socketListContainer;
        private Label summaryLabel;
        private VisualElement duplicateWarning;
        private PopupField<string> jumpPopup;

        private void OnEnable()
        {
            socketSystem = (ObjectSocketSystem)target;
            Selection.selectionChanged += OnEditorSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnEditorSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        public override VisualElement CreateInspectorGUI()
        {
            socketSystem = (ObjectSocketSystem)target;

            var root = new VisualElement();
            InventoryEditorStyleSheet.Apply(root);
            root.Add(BuildSocketSection());

            RebuildSocketList();
            return root;
        }

        private VisualElement BuildSocketSection()
        {
            var section = InventoryEditorUIFactory.CreateSection("Registered Sockets");
            section.style.marginTop = 4;

            duplicateWarning = new HelpBox(
                "동일한 이름의 ObjectSocket이 여러 개 있습니다. GameObject 이름을 고유하게 바꿔 주세요.",
                HelpBoxMessageType.Warning);
            duplicateWarning.style.display = DisplayStyle.None;
            duplicateWarning.style.marginBottom = 6;
            section.Add(duplicateWarning);

            section.Add(BuildToolbar());
            section.Add(BuildJumpRow());
            section.Add(BuildListHeader());

            socketListContainer = new VisualElement();
            section.Add(socketListContainer);

            return section;
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 6;

            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Refresh", RebuildSocketList));

            toolbar.Add(InventoryEditorUIFactory.CreateToolbarButton("Rebuild Cache", () =>
            {
                socketSystem.RebuildSocketCache();
                RebuildSocketList();
            }));

            summaryLabel = new Label { style = { flexGrow = 1, marginLeft = 8, fontSize = 11 } };
            toolbar.Add(summaryLabel);

            return toolbar;
        }

        private VisualElement BuildJumpRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            row.Add(new Label("Go to")
            {
                style = { width = 44, fontSize = 11 }
            });

            jumpPopup = new PopupField<string>(
                new List<string> { "(None)" },
                0,
                FormatJumpLabel,
                FormatJumpLabel)
            {
                style = { flexGrow = 1 }
            };
            jumpPopup.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == "(None)")
                    return;

                ObjectSocket socket = FindSocketByName(evt.newValue);
                if (socket != null)
                    SelectSocket(socket);
            });

            row.Add(jumpPopup);
            return row;
        }

        private static VisualElement BuildListHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingBottom = 4;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            header.style.marginBottom = 4;

            AddHeaderCell(header, "#", 24);
            AddHeaderCell(header, "Key", 96);
            AddHeaderCell(header, "Path", 140);
            AddHeaderCell(header, "Item", 52);
            AddHeaderCell(header, "", 52);

            return header;
        }

        private static void AddHeaderCell(VisualElement row, string text, float width)
        {
            row.Add(new Label(text)
            {
                style =
                {
                    width = width,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11
                }
            });
        }

        private void RebuildSocketList()
        {
            if (socketListContainer == null || socketSystem == null)
                return;

            ObjectSocket[] sockets = CollectSockets(socketSystem);
            HashSet<string> duplicateKeys = FindDuplicateKeys(sockets);

            socketListContainer.Clear();
            duplicateWarning.style.display = duplicateKeys.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            summaryLabel.text = sockets.Length == 0
                ? "ObjectSocket 없음"
                : $"{sockets.Length}개";

            RebuildJumpPopup(sockets);

            if (sockets.Length == 0)
            {
                socketListContainer.Add(new HelpBox(
                    "자식 GameObject에 ObjectSocket 컴포넌트를 추가하세요. 등록 키는 GameObject 이름입니다.",
                    HelpBoxMessageType.Info));
                return;
            }

            for (int i = 0; i < sockets.Length; i++)
            {
                ObjectSocket socket = sockets[i];
                bool isDuplicate = duplicateKeys.Contains(socket.name);
                bool isSelected = IsSocketSelected(socket);
                socketListContainer.Add(BuildSocketRow(socket, i, isDuplicate, isSelected));
            }
        }

        private VisualElement BuildSocketRow(ObjectSocket socket, int index, bool isDuplicate, bool isSelected)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;
            row.AddToClassList(InventoryEditorStyles.ListRowClass);

            if (isSelected)
                row.AddToClassList(InventoryEditorStyles.ListRowSelectedClass);

            if (isDuplicate)
                row.style.backgroundColor = new Color(0.55f, 0.2f, 0.15f, 0.25f);

            row.RegisterCallback<ClickEvent>(_ => SelectSocket(socket));

            row.Add(new Label(index.ToString())
            {
                style = { width = 24, fontSize = 11, color = new Color(0.7f, 0.7f, 0.7f) }
            });

            var keyLabel = new Label(socket.name)
            {
                style =
                {
                    width = 96,
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            if (isDuplicate)
                keyLabel.style.color = new Color(1f, 0.55f, 0.45f);
            row.Add(keyLabel);

            row.Add(new Label(GetHierarchyPath(socketSystem.transform, socket.transform))
            {
                style =
                {
                    width = 140,
                    fontSize = 10,
                    color = new Color(0.75f, 0.75f, 0.75f),
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap
                }
            });

            row.Add(new Label(GetAttachedLabel(socket))
            {
                style = { width = 52, fontSize = 10 }
            });

            var selectButton = InventoryEditorUIFactory.CreateToolbarButton("Select", () => SelectSocket(socket));
            selectButton.style.width = 52;
            selectButton.style.minWidth = 52;
            selectButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            row.Add(selectButton);

            return row;
        }

        private void RebuildJumpPopup(ObjectSocket[] sockets)
        {
            if (jumpPopup == null)
                return;

            var choices = new List<string> { "(None)" };
            choices.AddRange(sockets.Select(socket => socket.name));

            int currentIndex = 0;
            if (Selection.activeGameObject != null)
            {
                var activeSocket = Selection.activeGameObject.GetComponent<ObjectSocket>();
                if (activeSocket != null)
                {
                    int foundIndex = choices.IndexOf(activeSocket.name);
                    if (foundIndex >= 0)
                        currentIndex = foundIndex;
                }
            }

            jumpPopup.choices = choices;
            jumpPopup.SetValueWithoutNotify(choices[currentIndex]);
        }

        private ObjectSocket FindSocketByName(string socketName)
        {
            ObjectSocket[] sockets = CollectSockets(socketSystem);
            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i].name == socketName)
                    return sockets[i];
            }

            return null;
        }

        private static string FormatJumpLabel(string value) => value;

        private static ObjectSocket[] CollectSockets(ObjectSocketSystem system) =>
            system.GetComponentsInChildren<ObjectSocket>(true)
                .OrderBy(socket => socket.name, StringComparer.Ordinal)
                .ToArray();

        private static HashSet<string> FindDuplicateKeys(ObjectSocket[] sockets)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < sockets.Length; i++)
            {
                string key = sockets[i].name;
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!seen.Add(key))
                    duplicates.Add(key);
            }

            return duplicates;
        }

        private static string GetHierarchyPath(Transform root, Transform target)
        {
            if (target == null)
                return string.Empty;

            var parts = new List<string>();
            Transform current = target;

            while (current != null)
            {
                parts.Add(current.name);
                if (current == root)
                    break;

                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string GetAttachedLabel(ObjectSocket socket)
        {
            if (socket.HasItem && socket.TryGetItem<GameObjectSocketItem>(out GameObjectSocketItem socketItem))
                return socketItem.GameObject != null ? socketItem.GameObject.name : "item";

            return "—";
        }

        private static bool IsSocketSelected(ObjectSocket socket)
        {
            if (socket == null || Selection.activeGameObject == null)
                return false;

            Transform selected = Selection.activeGameObject.transform;
            return selected == socket.transform || selected.IsChildOf(socket.transform);
        }

        private static void SelectSocket(ObjectSocket socket)
        {
            if (socket == null)
                return;

            Selection.activeGameObject = socket.gameObject;
            EditorGUIUtility.PingObject(socket.gameObject);

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        private void OnEditorSelectionChanged() => RebuildSocketList();

        private void OnHierarchyChanged()
        {
            if (socketSystem == null)
                return;

            RebuildSocketList();
        }
    }
}
