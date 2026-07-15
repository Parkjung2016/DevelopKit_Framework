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
            if (GUILayout.Button("Open Montage Editor", GUILayout.Height(28)))
                AnimMontageEditorWindow.Open(montage);

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


    internal static class AnimMontageAssetOpenHandler
    {
        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            Object asset = EditorUtility.EntityIdToObject(entityId);
            switch (asset)
            {
                case AnimMontageSO montage:
                    AnimMontageEditorWindow.Open(montage);
                    return true;
                case AnimMontageLibrarySO library:
                    AnimMontageEditorWindow.Open(library);
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
            AnimMontageSOEditor.DrawHeader("Montage Library", library.name);
            AnimMontageSOEditor.DrawMetricRow(
                ("Montages", library.Montages.Count.ToString()),
                ("Preview", library.PreviewModel != null ? library.PreviewModel.name : "None"));

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open Montage Editor", GUILayout.Height(28)))
                AnimMontageEditorWindow.Open(library);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Montage Library 구성은 Montage Editor에서 관리합니다.", MessageType.Info);
        }
    }
}
