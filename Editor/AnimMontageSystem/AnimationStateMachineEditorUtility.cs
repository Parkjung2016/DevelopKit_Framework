using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class AnimationStateMachineEditorUtility
    {
        public static AnimStateMachineSO GetStateMachine(AnimMontageLibrarySO library) =>
            library != null ? library.StateMachine : null;

        public static void SetStateMachine(AnimMontageLibrarySO library, AnimStateMachineSO stateMachine)
        {
            if (library == null || library.StateMachine == stateMachine)
                return;

            Undo.RecordObject(library, "Set Animation State Machine");
            var serializedLibrary = new SerializedObject(library);
            serializedLibrary.FindProperty("stateMachine").objectReferenceValue = stateMachine;
            serializedLibrary.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
        }

        public static AnimStateMachineSO CreateWithSavePanel(AnimMontageLibrarySO library)
        {
            string libraryPath = library != null ? AssetDatabase.GetAssetPath(library) : string.Empty;
            string directory = string.IsNullOrEmpty(libraryPath)
                ? "Assets"
                : System.IO.Path.GetDirectoryName(libraryPath)?.Replace('\\', '/') ?? "Assets";
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Animation State Machine",
                library != null ? $"StateMachine_{library.name}" : "StateMachine_New",
                "asset",
                "Animation Sequence State와 Transition을 저장할 에셋을 만듭니다.",
                directory);
            if (string.IsNullOrEmpty(path))
                return null;

            var stateMachine = ScriptableObject.CreateInstance<AnimStateMachineSO>();
            AssetDatabase.CreateAsset(stateMachine, path);
            if (library != null)
                SetStateMachine(library, stateMachine);
            AssetDatabase.SaveAssets();
            return stateMachine;
        }

        public static void Open(AnimStateMachineSO stateMachine, AnimMontageLibrarySO library = null)
        {
            if (stateMachine != null)
                AnimationStateMachineEditorWindow.Open(stateMachine, library);
        }

        public static void Open(AnimationStateMachinePlayer player)
        {
            if (player?.StateMachine != null)
                AnimationStateMachineEditorWindow.Open(player);
        }

        public static int SyncSequenceStates(AnimMontageLibrarySO library)
        {
            AnimStateMachineSO stateMachine = GetStateMachine(library);
            if (stateMachine == null)
                return 0;

            Undo.RecordObject(stateMachine, "Sync Animation Sequence States");
            int changed = 0;
            for (int i = 0; i < library.Sequences.Count; i++)
            {
                AnimSequenceSO sequence = library.Sequences[i];
                if (sequence == null || stateMachine.FindState(sequence) != null)
                    continue;

                stateMachine.AddState(sequence, GetNextPosition(stateMachine.States.Count));
                changed++;
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(stateMachine);
                AssetDatabase.SaveAssetIfDirty(stateMachine);
            }
            return changed;
        }

        public static AnimSequenceState AddSequenceState(
            AnimMontageLibrarySO library,
            AnimSequenceSO sequence)
        {
            AnimStateMachineSO stateMachine = GetStateMachine(library);
            if (stateMachine == null || sequence == null)
                return null;

            AnimSequenceState existing = stateMachine.FindState(sequence);
            if (existing != null)
                return existing;

            Undo.RecordObject(stateMachine, "Add Animation Sequence State");
            AnimSequenceState state = stateMachine.AddState(sequence, GetNextPosition(stateMachine.States.Count));
            EditorUtility.SetDirty(stateMachine);
            AssetDatabase.SaveAssetIfDirty(stateMachine);
            return state;
        }

        private static Vector2 GetNextPosition(int index) => new(
            280f + index % 3 * 230f,
            100f + index / 3 * 120f);
    }
}
