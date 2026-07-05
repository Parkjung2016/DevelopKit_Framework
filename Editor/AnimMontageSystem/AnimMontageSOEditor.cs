using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [CustomEditor(typeof(AnimMontageSO))]
    public sealed class AnimMontageSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Montage Editor", GUILayout.Height(24)))
                    AnimMontageEditorWindow.Open((AnimMontageSO)target);

                if (GUILayout.Button("Rebuild Segment Times", GUILayout.Height(24)))
                {
                    Undo.RecordObject(target, "Rebuild Segment Times");
                    ((AnimMontageSO)target).RebuildSegmentStartTimes();
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}
