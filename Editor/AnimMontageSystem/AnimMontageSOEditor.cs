using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [CustomEditor(typeof(AnimMontageSO))]
    public sealed class AnimMontageSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var montage = (AnimMontageSO)target;
            DrawHeader("Animation Montage", montage.name);
            DrawMetricRow(
                ("Length", $"{montage.Length:0.###}s"),
                ("Segments", montage.Segments.Count.ToString()),
                ("Notifies", montage.Notifies.Count.ToString()),
                ("States", montage.NotifyStates.Count.ToString()));
            DrawMetricRow(
                ("Rate", montage.RateScale.ToString("0.###")),
                ("Root", montage.ApplyRootMotion ? "On" : "Off"),
                ("Tracks", GetTrackCount(montage).ToString()));

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open Animation Editor", GUILayout.Height(28)))
                AnimationEditorWindow.Open(montage);

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Rebuild Segment Times", GUILayout.Height(24)))
                {
                    Undo.RecordObject(target, "Rebuild Segment Times");
                    montage.RebuildSegmentStartTimes();
                    EditorUtility.SetDirty(target);
                }
            }

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play Mode에서는 Montage 에셋 편집을 잠급니다.", MessageType.Info);
        }

        private static int GetTrackCount(AnimMontageSO montage) =>
            montage.AnimationTracks.Count + montage.NotifyTracks.Count + montage.NotifyStateTracks.Count;

        internal static void DrawHeader(string title, string assetName)
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(assetName, EditorStyles.miniLabel);
            }
        }

        internal static void DrawMetricRow(params (string Label, string Value)[] metrics)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < metrics.Length; i++)
                    DrawMetric(metrics[i].Label, metrics[i].Value);
            }
        }

        private static void DrawMetric(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(64)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            }
        }
    }


    [CustomEditor(typeof(AnimSequenceSO))]
    public sealed class AnimSequenceSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var sequence = (AnimSequenceSO)target;
            AnimMontageSOEditor.DrawHeader("Animation Sequence", sequence.name);

            EditorGUI.BeginChangeCheck();
            AnimationClip clip = (AnimationClip)EditorGUILayout.ObjectField(
                "Animation Clip",
                sequence.Clip,
                typeof(AnimationClip),
                false);
            if (EditorGUI.EndChangeCheck())
                AnimationSequenceEditorUtility.SetClip(sequence, clip);

            AnimMontageSOEditor.DrawMetricRow(
                ("Length", $"{sequence.Length:0.###}s"),
                ("Notifies", sequence.Notifies.Count.ToString()),
                ("States", sequence.NotifyStates.Count.ToString()));

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open Animation Editor", GUILayout.Height(28)))
                AnimationEditorWindow.Open(sequence);

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play Mode에서는 Animation 에셋 편집을 잠급니다.", MessageType.Info);
        }
    }
    internal static class AnimMontageAssetOpenHandler
    {
        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            Object asset = EditorUtility.EntityIdToObject(entityId);
            switch (asset)
            {
                case AnimSequenceSO sequence:
                    AnimationEditorWindow.Open(sequence);
                    return true;
                case AnimMontageSO montage:
                    AnimationEditorWindow.Open(montage);
                    return true;
                case AnimMontageLibrarySO library:
                    AnimationEditorWindow.Open(library);
                    return true;
                case AnimStateMachineSO stateMachine:
                    AnimationStateMachineEditorUtility.Open(stateMachine);
                    return true;
                default:
                    return false;
            }
        }
    }
    [CustomEditor(typeof(AnimMontageLibrarySO))]
    public sealed class AnimMontageLibrarySOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var library = (AnimMontageLibrarySO)target;
            AnimMontageSOEditor.DrawHeader("Animation Library", library.name);
            AnimMontageSOEditor.DrawMetricRow(
                ("Sequences", library.Sequences.Count.ToString()),
                ("Montages", library.Montages.Count.ToString()),
                ("Preview", library.PreviewModel != null ? library.PreviewModel.name : "None"));

            EditorGUILayout.Space(8);
            AnimStateMachineSO stateMachine = AnimationStateMachineEditorUtility.GetStateMachine(library);
            EditorGUI.BeginChangeCheck();
            stateMachine = (AnimStateMachineSO)EditorGUILayout.ObjectField(
                "State Machine",
                stateMachine,
                typeof(AnimStateMachineSO),
                false);
            if (EditorGUI.EndChangeCheck())
                AnimationStateMachineEditorUtility.SetStateMachine(library, stateMachine);

            stateMachine = AnimationStateMachineEditorUtility.GetStateMachine(library);
            if (stateMachine == null)
            {
                if (GUILayout.Button("Create State Machine", GUILayout.Height(24)))
                    AnimationStateMachineEditorUtility.CreateWithSavePanel(library);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Sync Sequence States", GUILayout.Height(24)))
                        AnimationStateMachineEditorUtility.SyncSequenceStates(library);
                    if (GUILayout.Button("Open State Machine", GUILayout.Height(24)))
                        AnimationStateMachineEditorUtility.Open(stateMachine, library);
                }
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open Animation Editor", GUILayout.Height(28)))
                AnimationEditorWindow.Open(library);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Sequence를 State로 직접 연결하고 Transition과 조건을 전용 그래프에서 편집합니다.",
                MessageType.Info);
        }
    }
}
