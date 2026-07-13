using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [CustomEditor(typeof(ObjectAnimMontagePlayer))]
    internal sealed class ObjectAnimMontagePlayerEditor : Editor
    {
        private SerializedProperty animatorProperty;
        private SerializedProperty rootMotionModeProperty;
        private SerializedProperty rootMotionRigidbodyProperty;
        private SerializedProperty rootMotionCharacterControllerProperty;
        private SerializedProperty customRootMotionControllerProperty;

        private void OnEnable()
        {
            animatorProperty = serializedObject.FindProperty("animator");
            rootMotionModeProperty = serializedObject.FindProperty("rootMotionMode");
            rootMotionRigidbodyProperty = serializedObject.FindProperty("rootMotionRigidbody");
            rootMotionCharacterControllerProperty = serializedObject.FindProperty("rootMotionCharacterController");
            customRootMotionControllerProperty = serializedObject.FindProperty("customRootMotionController");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Montage Player", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(animatorProperty);

            EditorGUILayout.Space(8f);
            DrawRootMotionSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRootMotionSection()
        {
            EditorGUILayout.LabelField("Root Motion", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(rootMotionModeProperty, new GUIContent("Mode"));

            MontageRootMotionMode mode = (MontageRootMotionMode)rootMotionModeProperty.enumValueIndex;
            using (new EditorGUI.IndentLevelScope())
            {
                switch (mode)
                {
                    case MontageRootMotionMode.Rigidbody:
                        EditorGUILayout.PropertyField(rootMotionRigidbodyProperty, new GUIContent("Rigidbody"));
                        break;
                    case MontageRootMotionMode.CharacterController:
                        EditorGUILayout.PropertyField(rootMotionCharacterControllerProperty, new GUIContent("Character Controller"));
                        break;
                    case MontageRootMotionMode.Custom:
                        EditorGUILayout.PropertyField(customRootMotionControllerProperty, new GUIContent("Controller"));
                        break;
                }
            }

            DrawModeHelp(mode);
        }

        private static void DrawModeHelp(MontageRootMotionMode mode)
        {
            string message = mode switch
            {
                MontageRootMotionMode.Rigidbody => "Root Motion is applied with Rigidbody.MovePosition and MoveRotation.",
                MontageRootMotionMode.CharacterController => "Root Motion is applied with CharacterController.Move and transform rotation.",
                MontageRootMotionMode.Custom => "Assign a MontageRootMotionController to handle Root Motion deltas yourself.",
                _ => "Root Motion is applied directly to the Animator transform."
            };

            EditorGUILayout.HelpBox(message, MessageType.None);
        }
    }
}