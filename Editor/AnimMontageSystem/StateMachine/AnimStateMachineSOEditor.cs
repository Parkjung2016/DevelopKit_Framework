using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [CustomEditor(typeof(AnimStateMachineSO))]
    internal sealed class AnimStateMachineSOEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var stateMachine = (AnimStateMachineSO)target;
            VisualElement root = StateMachineInspectorUI.CreateRoot();
            root.Add(StateMachineInspectorUI.CreateHeader(
                "Animation State Machine", stateMachine.name));
            root.Add(StateMachineInspectorUI.CreateMetrics(
                ("States", stateMachine.States.Count),
                ("Transitions", stateMachine.Transitions.Count),
                ("Parameters", stateMachine.Parameters.Count)));

            Button openButton = StateMachineInspectorUI.CreateButton(
                "Open State Machine", () => AnimationStateMachineEditorUtility.Open(stateMachine), true);
            openButton.style.height = 30f;
            root.Add(openButton);
            root.Add(new HelpBox(
                "State, Conduit, Alias, 내부 State Machine과 Transition Rule은 전용 편집기에서 설정합니다.",
                HelpBoxMessageType.Info));
            return root;
        }
    }

    [CustomEditor(typeof(AnimationStateMachinePlayer))]
    internal sealed class AnimationStateMachinePlayerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var player = (AnimationStateMachinePlayer)target;
            SerializedProperty animatorProperty = serializedObject.FindProperty("animator");
            SerializedProperty stateMachineProperty = serializedObject.FindProperty("stateMachine");
            SerializedProperty playOnEnableProperty = serializedObject.FindProperty("playOnEnable");
            SerializedProperty createAnimatorProperty = serializedObject.FindProperty("createAnimatorIfMissing");

            VisualElement root = StateMachineInspectorUI.CreateRoot();
            Label subtitle = StateMachineInspectorUI.CreateHeader(
                root, "Animation State Machine Player", "Not Configured");

            root.Add(StateMachineInspectorUI.CreateSection("State Machine"));
            var stateMachineField = new PropertyField(stateMachineProperty, "Asset");
            root.Add(stateMachineField);

            Button openButton = StateMachineInspectorUI.CreateButton("Open Editor", () =>
            {
                serializedObject.ApplyModifiedProperties();
                AnimationStateMachineEditorUtility.Open(player);
            }, true);
            Button createButton = StateMachineInspectorUI.CreateButton("Create New", () =>
            {
                AnimStateMachineSO created = AnimationStateMachineEditorUtility.CreateWithSavePanel(null);
                if (created == null)
                    return;

                serializedObject.Update();
                stateMachineProperty.objectReferenceValue = created;
                serializedObject.ApplyModifiedProperties();
                AnimationStateMachineEditorUtility.Open(created);
            });
            root.Add(StateMachineInspectorUI.CreateActionRow(openButton, createButton));
            var stateMachineMessage = new HelpBox(string.Empty, HelpBoxMessageType.None);
            root.Add(stateMachineMessage);

            root.Add(StateMachineInspectorUI.CreateSection("Animation Output"));
            var animatorField = new PropertyField(animatorProperty, "Animator");
            var createAnimatorField = new PropertyField(createAnimatorProperty, "Create If Missing");
            root.Add(animatorField);
            root.Add(createAnimatorField);

            Button findAnimatorButton = StateMachineInspectorUI.CreateButton("Find In Children", () =>
            {
                Animator found = player.GetComponentInChildren<Animator>(true);
                if (found == null)
                    return;

                serializedObject.Update();
                animatorProperty.objectReferenceValue = found;
                serializedObject.ApplyModifiedProperties();
            });
            Button addAnimatorButton = StateMachineInspectorUI.CreateButton("Add Animator", () =>
            {
                Animator added = Undo.AddComponent<Animator>(player.gameObject);
                serializedObject.Update();
                animatorProperty.objectReferenceValue = added;
                serializedObject.ApplyModifiedProperties();
            });
            root.Add(StateMachineInspectorUI.CreateActionRow(findAnimatorButton, addAnimatorButton));
            var animatorMessage = new HelpBox(string.Empty, HelpBoxMessageType.None);
            root.Add(animatorMessage);

            root.Add(StateMachineInspectorUI.CreateSection("Playback"));
            var playOnEnableField = new PropertyField(playOnEnableProperty, "Play On Enable");
            root.Add(playOnEnableField);

            VisualElement livePanel = StateMachineInspectorUI.CreateLivePanel();
            Label currentState = new();
            Label stateTime = new();
            Label transition = new();
            Label status = new();
            livePanel.Add(currentState);
            livePanel.Add(stateTime);
            livePanel.Add(transition);
            livePanel.Add(status);
            root.Add(livePanel);

            Button rebuildButton = StateMachineInspectorUI.CreateButton("Rebuild", player.Build);
            Button playButton = StateMachineInspectorUI.CreateButton("Play Default", player.PlayDefault);
            VisualElement playbackActions = StateMachineInspectorUI.CreateActionRow(rebuildButton, playButton);
            root.Add(playbackActions);

            void Refresh()
            {
                if (player == null)
                    return;

                serializedObject.UpdateIfRequiredOrScript();
                var machine = stateMachineProperty.objectReferenceValue as AnimStateMachineSO;
                var animator = animatorProperty.objectReferenceValue as Animator;
                subtitle.text = machine != null ? machine.name : "Not Configured";
                openButton.text = Application.isPlaying ? "Open Live Debugger" : "Open Editor";
                openButton.SetEnabled(machine != null);

                stateMachineMessage.style.display = DisplayStyle.Flex;
                if (machine == null)
                {
                    stateMachineMessage.text = "State Machine을 만들거나 할당하면 바로 사용할 수 있습니다.";
                    stateMachineMessage.messageType = HelpBoxMessageType.Info;
                }
                else if (string.IsNullOrEmpty(machine.DefaultNodeId))
                {
                    stateMachineMessage.text = "기본 State가 없습니다. 편집기에서 Default State를 지정하세요.";
                    stateMachineMessage.messageType = HelpBoxMessageType.Warning;
                }
                else
                {
                    stateMachineMessage.style.display = DisplayStyle.None;
                }

                animatorMessage.style.display = DisplayStyle.Flex;
                if (animator == null)
                {
                    animatorMessage.text = "Animator를 할당하거나 자식에서 찾으세요. Animator Controller는 필요하지 않습니다.";
                    animatorMessage.messageType = HelpBoxMessageType.Info;
                }
                else if (animator.runtimeAnimatorController != null)
                {
                    animatorMessage.text = "이 Player가 재생하는 동안에는 할당된 Animator Controller를 사용하지 않습니다.";
                    animatorMessage.messageType = HelpBoxMessageType.Info;
                }
                else if (animator.isHuman && animator.avatar == null)
                {
                    animatorMessage.text = "Humanoid Animator에 Avatar가 없습니다.";
                    animatorMessage.messageType = HelpBoxMessageType.Warning;
                }
                else
                {
                    animatorMessage.style.display = DisplayStyle.None;
                }

                bool canConfigure = !Application.isPlaying;
                stateMachineField.SetEnabled(canConfigure);
                createButton.SetEnabled(canConfigure);
                animatorField.SetEnabled(canConfigure);
                createAnimatorField.SetEnabled(canConfigure);
                findAnimatorButton.SetEnabled(canConfigure);
                addAnimatorButton.SetEnabled(canConfigure && animator == null);
                playOnEnableField.SetEnabled(canConfigure);

                livePanel.style.display = Application.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
                playbackActions.style.display = Application.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
                if (!Application.isPlaying)
                    return;

                currentState.text = $"Current State    {player.CurrentState?.Name ?? "None"}";
                stateTime.text = $"State Time       {player.StateTime:0.###}s";
                transition.text = player.IsTransitioning
                    ? $"Transition       {player.NextState?.Name ?? "None"}  {player.TransitionProgress * 100f:0}%"
                    : "Transition       None";
                status.text = $"Status           {(player.IsReady ? "Running" : "Not Ready")}";
            }

            stateMachineField.RegisterCallback<SerializedPropertyChangeEvent>(_ => Refresh());
            animatorField.RegisterCallback<SerializedPropertyChangeEvent>(_ => Refresh());
            root.schedule.Execute(Refresh).Every(200);
            Refresh();
            return root;
        }

        [MenuItem("CONTEXT/AnimationStateMachinePlayer/Open State Machine Editor")]
        private static void OpenFromContext(MenuCommand command)
        {
            if (command.context is AnimationStateMachinePlayer { StateMachine: not null } player)
                AnimationStateMachineEditorUtility.Open(player);
        }

        [MenuItem("CONTEXT/AnimationStateMachinePlayer/Open State Machine Editor", true)]
        private static bool CanOpenFromContext(MenuCommand command) =>
            command.context is AnimationStateMachinePlayer { StateMachine: not null };
    }

    internal static class StateMachineInspectorUI
    {
        private static readonly Color PanelColor = EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.165f, 0.18f)
            : new Color(0.83f, 0.84f, 0.86f);

        public static VisualElement CreateRoot()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            return root;
        }

        public static VisualElement CreateHeader(string title, string subtitle)
        {
            var root = new VisualElement();
            CreateHeader(root, title, subtitle);
            return root;
        }

        public static Label CreateHeader(VisualElement root, string title, string subtitle)
        {
            var header = new VisualElement();
            header.style.paddingLeft = 8f;
            header.style.paddingRight = 8f;
            header.style.paddingTop = 7f;
            header.style.paddingBottom = 7f;
            header.style.backgroundColor = PanelColor;
            header.style.borderBottomLeftRadius = 4f;
            header.style.borderBottomRightRadius = 4f;
            header.style.borderTopLeftRadius = 4f;
            header.style.borderTopRightRadius = 4f;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 13f;
            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.opacity = 0.7f;
            subtitleLabel.style.marginTop = 2f;
            header.Add(titleLabel);
            header.Add(subtitleLabel);
            root.Add(header);
            return subtitleLabel;
        }

        public static VisualElement CreateMetrics(params (string Label, string Value)[] values)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 2f;
            for (int i = 0; i < values.Length; i++)
            {
                var item = new VisualElement();
                item.style.flexGrow = 1f;
                item.style.minWidth = 76f;
                item.style.paddingTop = 5f;
                item.style.paddingBottom = 5f;
                item.style.marginRight = i < values.Length - 1 ? 4f : 0f;
                item.style.backgroundColor = PanelColor;
                var value = new Label(values[i].Value)
                {
                    style = { unityTextAlign = TextAnchor.MiddleCenter, unityFontStyleAndWeight = FontStyle.Bold }
                };
                var label = new Label(values[i].Label)
                {
                    style = { unityTextAlign = TextAnchor.MiddleCenter, opacity = 0.65f, fontSize = 10f }
                };
                item.Add(value);
                item.Add(label);
                row.Add(item);
            }
            return row;
        }

        public static VisualElement CreateMetrics(params (string Label, int Value)[] values)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 2f;
            for (int i = 0; i < values.Length; i++)
            {
                var item = new VisualElement();
                item.style.flexGrow = 1f;
                item.style.minWidth = 76f;
                item.style.paddingTop = 5f;
                item.style.paddingBottom = 5f;
                item.style.marginRight = i < values.Length - 1 ? 4f : 0f;
                item.style.backgroundColor = PanelColor;
                var value = new Label(values[i].Value.ToString())
                {
                    style = { unityTextAlign = TextAnchor.MiddleCenter, unityFontStyleAndWeight = FontStyle.Bold }
                };
                var label = new Label(values[i].Label)
                {
                    style = { unityTextAlign = TextAnchor.MiddleCenter, opacity = 0.65f, fontSize = 10f }
                };
                item.Add(value);
                item.Add(label);
                row.Add(item);
            }
            return row;
        }

        public static Label CreateSection(string title)
        {
            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8f;
            label.style.paddingBottom = 2f;
            label.style.borderBottomWidth = 1f;
            label.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            return label;
        }

        public static Button CreateButton(string text, System.Action clicked, bool emphasized = false)
        {
            var button = new Button(clicked) { text = text };
            button.style.height = 25f;
            button.style.minWidth = 100f;
            button.style.flexGrow = 1f;
            if (emphasized)
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }

        public static VisualElement CreateActionRow(params Button[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 2f;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].style.marginRight = i < buttons.Length - 1 ? 4f : 0f;
                buttons[i].style.marginBottom = 2f;
                row.Add(buttons[i]);
            }
            return row;
        }

        public static VisualElement CreateLivePanel()
        {
            var panel = new VisualElement();
            panel.style.paddingLeft = 8f;
            panel.style.paddingRight = 8f;
            panel.style.paddingTop = 6f;
            panel.style.paddingBottom = 6f;
            panel.style.backgroundColor = PanelColor;
            panel.style.borderBottomLeftRadius = 4f;
            panel.style.borderBottomRightRadius = 4f;
            panel.style.borderTopLeftRadius = 4f;
            panel.style.borderTopRightRadius = 4f;
            return panel;
        }
    }
}