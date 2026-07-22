using PJDev.UI;
using UnityEditor;
using UnityEngine;

namespace PJDev.UI.Editor
{
    [CustomEditor(typeof(SafeArea))]
    internal sealed class SafeAreaEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("controlEdges"));

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Screen.safeArea에 맞춰 앵커를 조정합니다.\n" +
                "에디터에서는 Window > General > Device Simulator로 미리볼 수 있습니다.",
                MessageType.Info);

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (Object targetObject in targets)
                {
                    if (targetObject is SafeArea safeArea)
                        safeArea.Refresh();
                }
            }
        }
    }
}
