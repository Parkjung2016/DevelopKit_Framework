using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [CustomEditor(typeof(AnimStateMachineSO))]
    internal sealed class AnimStateMachineSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var stateMachine = (AnimStateMachineSO)target;
            AnimMontageSOEditor.DrawHeader("Animation State Machine", stateMachine.name);
            AnimMontageSOEditor.DrawMetricRow(
                ("States", stateMachine.States.Count.ToString()),
                ("Transitions", stateMachine.Transitions.Count.ToString()),
                ("Parameters", stateMachine.Parameters.Count.ToString()));

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Open State Machine", GUILayout.Height(30f)))
                AnimationStateMachineEditorUtility.Open(stateMachine);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "State, 조건 분기(Branch), 공유 State 그룹, 내부 State Machine은 전용 그래프에서 편집합니다.",
                MessageType.Info);
        }
    }
    [CustomEditor(typeof(AnimationStateMachinePlayer))]
    internal sealed class AnimationStateMachinePlayerEditor : UnityEditor.Editor
    {
        private SerializedProperty animatorProperty;
        private SerializedProperty stateMachineProperty;
        private SerializedProperty playOnEnableProperty;
        private SerializedProperty createAnimatorProperty;

        private void OnEnable()
        {
            animatorProperty = serializedObject.FindProperty("animator");
            stateMachineProperty = serializedObject.FindProperty("stateMachine");
            playOnEnableProperty = serializedObject.FindProperty("playOnEnable");
            createAnimatorProperty = serializedObject.FindProperty("createAnimatorIfMissing");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var player = (AnimationStateMachinePlayer)target;
            var stateMachine = stateMachineProperty.objectReferenceValue as AnimStateMachineSO;
            var animator = animatorProperty.objectReferenceValue as Animator;

            AnimMontageSOEditor.DrawHeader("Animation State Machine Player",
                stateMachine != null ? stateMachine.name : "Not Configured");

            DrawStateMachineSetup(player, stateMachine);
            EditorGUILayout.Space(8f);
            DrawAnimatorSetup(player, animator);
            EditorGUILayout.Space(8f);
            DrawPlayback(player);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStateMachineSetup(
            AnimationStateMachinePlayer player,
            AnimStateMachineSO stateMachine)
        {
            EditorGUILayout.LabelField("State Machine", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(stateMachineProperty, GUIContent.none);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(stateMachine == null))
                {
                    string openLabel = Application.isPlaying ? "Open Live Debugger" : "Open Editor";
                    if (GUILayout.Button(openLabel, GUILayout.Height(28f)))
                        AnimationStateMachineEditorUtility.Open(player);
                }

                if (GUILayout.Button(stateMachine == null ? "Create & Open" : "Create New",
                        GUILayout.Height(28f)))
                {
                    AnimStateMachineSO created = AnimationStateMachineEditorUtility.CreateWithSavePanel(null);
                    if (created != null)
                    {
                        stateMachineProperty.objectReferenceValue = created;
                        serializedObject.ApplyModifiedProperties();
                        AnimationStateMachineEditorUtility.Open(created);
                    }
                }
            }

            if (stateMachine == null)
                EditorGUILayout.HelpBox("State Machine을 만들거나 할당하면 바로 사용할 수 있습니다.",
                    MessageType.Info);
            else if (string.IsNullOrEmpty(stateMachine.DefaultNodeId))
                EditorGUILayout.HelpBox("기본 State가 없습니다. 편집기에서 Default State를 지정하세요.",
                    MessageType.Warning);
        }

        private void DrawAnimatorSetup(AnimationStateMachinePlayer player, Animator animator)
        {
            EditorGUILayout.LabelField("Animation Output", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(animatorProperty, new GUIContent("Animator"));
            EditorGUILayout.PropertyField(createAnimatorProperty,
                new GUIContent("Create If Missing"));

            if (animator == null)
            {
                EditorGUILayout.HelpBox(
                    "Animator는 Playables가 모델에 포즈를 적용할 때만 사용합니다. Animator Controller는 필요하지 않습니다.",
                    MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Find In Children", GUILayout.Height(24f)))
                    {
                        Animator found = player.GetComponentInChildren<Animator>(true);
                        if (found != null)
                        {
                            animatorProperty.objectReferenceValue = found;
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                    if (GUILayout.Button("Add Animator", GUILayout.Height(24f)))
                    {
                        Animator added = Undo.AddComponent<Animator>(player.gameObject);
                        animatorProperty.objectReferenceValue = added;
                        serializedObject.ApplyModifiedProperties();
                    }
                }
                return;
            }

            if (animator.runtimeAnimatorController != null)
                EditorGUILayout.HelpBox(
                    "Animator Controller가 할당되어 있지만 이 Player가 활성화된 동안에는 사용하지 않습니다.",
                    MessageType.None);
            if (animator.avatar == null && animator.isHuman)
                EditorGUILayout.HelpBox("Humanoid Animator에 Avatar가 없습니다.", MessageType.Warning);
        }

        private void DrawPlayback(AnimationStateMachinePlayer player)
        {
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playOnEnableProperty, new GUIContent("Play On Enable"));

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space(4f);
            string stateName = player.CurrentState?.Name ?? "None";
            EditorGUILayout.LabelField("Current State", stateName);
            EditorGUILayout.LabelField("State Time", $"{player.StateTime:0.###}s");
            if (player.IsTransitioning)
            {
                EditorGUILayout.LabelField("Next State", player.NextState?.Name ?? "None");
                EditorGUILayout.LabelField("Transition", $"{player.TransitionProgress * 100f:0}%");
            }
            EditorGUILayout.LabelField("Status", player.IsReady ? "Running" : "Not Ready");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild", GUILayout.Height(24f)))
                    player.Build();
                if (GUILayout.Button("Play Default", GUILayout.Height(24f)))
                    player.PlayDefault();
            }
            Repaint();
        }

        [MenuItem("CONTEXT/AnimationStateMachinePlayer/Open State Machine Editor")]
        private static void OpenFromContext(MenuCommand command)
        {
            var player = command.context as AnimationStateMachinePlayer;
            if (player?.StateMachine != null)
                AnimationStateMachineEditorUtility.Open(player);
        }

        [MenuItem("CONTEXT/AnimationStateMachinePlayer/Open State Machine Editor", true)]
        private static bool CanOpenFromContext(MenuCommand command) =>
            command.context is AnimationStateMachinePlayer { StateMachine: not null };
    }
}
