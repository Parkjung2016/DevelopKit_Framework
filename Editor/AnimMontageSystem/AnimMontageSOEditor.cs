using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [CustomEditor(typeof(AnimMontageSO))]
    public sealed class AnimMontageSOEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var montage = (AnimMontageSO)target;
            VisualElement root = StateMachineInspectorUI.CreateRoot();
            root.Add(StateMachineInspectorUI.CreateHeader("Animation Montage", montage.name));
            root.Add(StateMachineInspectorUI.CreateMetrics(
                ("Length", $"{montage.Length:0.###}s"),
                ("Segments", montage.Segments.Count.ToString()),
                ("Notifies", montage.Notifies.Count.ToString()),
                ("States", montage.NotifyStates.Count.ToString())));
            root.Add(StateMachineInspectorUI.CreateMetrics(
                ("Rate", montage.RateScale.ToString("0.###")),
                ("Root Motion", montage.ApplyRootMotion ? "On" : "Off"),
                ("Tracks", GetTrackCount(montage).ToString())));

            Button openButton = StateMachineInspectorUI.CreateButton(
                "Open Animation Editor", () => AnimationEditorWindow.Open(montage), true);
            openButton.style.height = 30f;
            root.Add(openButton);

            Button rebuildButton = StateMachineInspectorUI.CreateButton("Rebuild Segment Times", () =>
            {
                Undo.RecordObject(montage, "Rebuild Segment Times");
                montage.RebuildSegmentStartTimes();
                EditorUtility.SetDirty(montage);
            });
            root.Add(rebuildButton);

            var playModeMessage = new HelpBox(
                "Play Mode에서는 Montage 에셋을 편집할 수 없습니다.", HelpBoxMessageType.Info);
            root.Add(playModeMessage);
            root.schedule.Execute(() =>
            {
                rebuildButton.SetEnabled(!EditorApplication.isPlaying);
                playModeMessage.style.display = EditorApplication.isPlaying
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }).Every(250);
            playModeMessage.style.display = EditorApplication.isPlaying
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            return root;
        }

        private static int GetTrackCount(AnimMontageSO montage) =>
            montage.AnimationTracks.Count + montage.NotifyTracks.Count + montage.NotifyStateTracks.Count;
    }

    [CustomEditor(typeof(AnimSequenceSO))]
    public sealed class AnimSequenceSOEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var sequence = (AnimSequenceSO)target;
            VisualElement root = StateMachineInspectorUI.CreateRoot();
            root.Add(StateMachineInspectorUI.CreateHeader("Animation Sequence", sequence.name));

            var clipField = new ObjectField("Animation Clip")
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
                value = sequence.Clip
            };
            clipField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != sequence.Clip)
                    AnimationSequenceEditorUtility.SetClip(sequence, evt.newValue as AnimationClip);
            });
            root.Add(clipField);
            root.Add(StateMachineInspectorUI.CreateMetrics(
                ("Length", $"{sequence.Length:0.###}s"),
                ("Notifies", sequence.Notifies.Count.ToString()),
                ("States", sequence.NotifyStates.Count.ToString())));

            Button openButton = StateMachineInspectorUI.CreateButton(
                "Open Animation Editor", () => AnimationEditorWindow.Open(sequence), true);
            openButton.style.height = 30f;
            root.Add(openButton);

            var playModeMessage = new HelpBox(
                "Play Mode에서는 Animation 에셋을 편집할 수 없습니다.", HelpBoxMessageType.Info);
            root.Add(playModeMessage);
            root.schedule.Execute(() =>
            {
                clipField.SetEnabled(!EditorApplication.isPlaying);
                playModeMessage.style.display = EditorApplication.isPlaying
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }).Every(250);
            playModeMessage.style.display = EditorApplication.isPlaying
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            return root;
        }
    }

    [CustomEditor(typeof(AnimMontageLibrarySO))]
    public sealed class AnimMontageLibrarySOEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var library = (AnimMontageLibrarySO)target;
            VisualElement root = StateMachineInspectorUI.CreateRoot();
            root.Add(StateMachineInspectorUI.CreateHeader("Animation Library", library.name));
            root.Add(StateMachineInspectorUI.CreateMetrics(
                ("Sequences", library.Sequences.Count.ToString()),
                ("Montages", library.Montages.Count.ToString()),
                ("Preview", library.PreviewModel != null ? library.PreviewModel.name : "None")));

            root.Add(StateMachineInspectorUI.CreateSection("State Machine"));
            var stateMachineField = new ObjectField("Asset")
            {
                objectType = typeof(AnimStateMachineSO),
                allowSceneObjects = false,
                value = AnimationStateMachineEditorUtility.GetStateMachine(library)
            };
            root.Add(stateMachineField);

            Button createButton = StateMachineInspectorUI.CreateButton(
                "Create State Machine", () =>
                {
                    AnimStateMachineSO created = AnimationStateMachineEditorUtility.CreateWithSavePanel(library);
                    if (created != null)
                        stateMachineField.SetValueWithoutNotify(created);
                }, true);
            Button syncButton = StateMachineInspectorUI.CreateButton(
                "Sync Sequence States", () => AnimationStateMachineEditorUtility.SyncSequenceStates(library));
            Button openStateMachineButton = StateMachineInspectorUI.CreateButton(
                "Open State Machine", () => AnimationStateMachineEditorUtility.Open(
                    AnimationStateMachineEditorUtility.GetStateMachine(library), library), true);
            VisualElement stateMachineActions = StateMachineInspectorUI.CreateActionRow(
                createButton, syncButton, openStateMachineButton);
            root.Add(stateMachineActions);

            stateMachineField.RegisterValueChangedCallback(evt =>
            {
                AnimationStateMachineEditorUtility.SetStateMachine(library, evt.newValue as AnimStateMachineSO);
                RefreshActions();
            });

            root.Add(StateMachineInspectorUI.CreateSection("Editor"));
            Button openEditorButton = StateMachineInspectorUI.CreateButton(
                "Open Animation Editor", () => AnimationEditorWindow.Open(library), true);
            openEditorButton.style.height = 30f;
            root.Add(openEditorButton);
            root.Add(new HelpBox(
                "Sequence는 State Machine의 State로 연결할 수 있으며, Transition과 Rule은 전용 편집기에서 설정합니다.",
                HelpBoxMessageType.Info));

            void RefreshActions()
            {
                AnimStateMachineSO machine = AnimationStateMachineEditorUtility.GetStateMachine(library);
                bool hasMachine = machine != null;
                createButton.style.display = hasMachine ? DisplayStyle.None : DisplayStyle.Flex;
                syncButton.style.display = hasMachine ? DisplayStyle.Flex : DisplayStyle.None;
                openStateMachineButton.style.display = hasMachine ? DisplayStyle.Flex : DisplayStyle.None;
                bool canEdit = !EditorApplication.isPlaying;
                stateMachineField.SetEnabled(canEdit);
                createButton.SetEnabled(canEdit);
                syncButton.SetEnabled(canEdit);
            }

            root.schedule.Execute(RefreshActions).Every(250);
            RefreshActions();
            return root;
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
}