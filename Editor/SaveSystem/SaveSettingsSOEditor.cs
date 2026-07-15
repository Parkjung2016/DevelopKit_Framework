using System;
using PJDev.DevelopKit.Framework.SaveSystem.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.SaveSystem
{
    [CustomEditor(typeof(SaveSettingsSO))]
    internal sealed class SaveSettingsSOEditor : Editor
    {
        private SerializedProperty encryptionEnabled;
        private SerializedProperty encryptionPassword;
        private SerializedProperty folderName;
        private SerializedProperty fileExtension;

        private void OnEnable()
        {
            encryptionEnabled = serializedObject.FindProperty("encryptionEnabled");
            encryptionPassword = serializedObject.FindProperty("encryptionPassword");
            folderName = serializedObject.FindProperty("folderName");
            fileExtension = serializedObject.FindProperty("fileExtension");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Save Location", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                folderName,
                new GUIContent(
                    "Folder Name",
                    "Application.persistentDataPath 아래에 생성할 폴더 이름입니다."));
            EditorGUILayout.PropertyField(
                fileExtension,
                new GUIContent("File Extension", "저장 파일에 사용할 확장자입니다."));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Encryption", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                encryptionEnabled,
                new GUIContent("Enable Encryption", "저장 파일을 암호화합니다."));

            if (encryptionEnabled.boolValue)
            {
                EditorGUI.BeginChangeCheck();
                string password = EditorGUILayout.PasswordField(
                    new GUIContent(
                        "Encryption Password",
                        "프로젝트 전용 비밀번호입니다. 배포 후 변경하면 기존 저장 파일을 열 수 없습니다."),
                    encryptionPassword.stringValue);

                if (EditorGUI.EndChangeCheck())
                    encryptionPassword.stringValue = password;

                if (string.IsNullOrWhiteSpace(encryptionPassword.stringValue))
                {
                    EditorGUILayout.HelpBox(
                        "암호화를 사용하려면 Encryption Password를 입력해야 합니다.",
                        MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawResolvedDirectory();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open Save Slot Browser", GUILayout.Height(24)))
                SaveBrowserWindow.Open((SaveSettingsSO)target);
        }

        private void DrawResolvedDirectory()
        {
            try
            {
                string directory = ((SaveSettingsSO)target).SaveDirectory;
                EditorGUILayout.LabelField("Resolved Directory", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    directory,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }
    }

    internal static class SaveSettingsAssetOpenHandler
    {
        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not SaveSettingsSO settings)
                return false;

            SaveBrowserWindow.Open(settings);
            return true;
        }
    }
}