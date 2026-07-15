using System;
using System.IO;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    [CreateAssetMenu(fileName = "SaveSettings", menuName = "PJDev/Save System/Settings")]
    public sealed class SaveSettingsSO : ScriptableObject
    {
        private const string DefaultFolderName = "Saves";

        [SerializeField]
        [Tooltip("저장 파일을 암호화합니다. 활성화하면 Encryption Password가 필요합니다.")]
        private bool encryptionEnabled = false;

        [SerializeField]
        [Tooltip("저장 파일 암호화에 사용할 프로젝트 전용 비밀번호입니다.")]
        private string encryptionPassword = string.Empty;

        [SerializeField]
        [Tooltip("Application.persistentDataPath 아래에 생성할 폴더 이름입니다.")]
        private string folderName = DefaultFolderName;

        [SerializeField]
        [Tooltip("저장 파일에 사용할 확장자입니다.")]
        private string fileExtension = ".sav";

        public bool EncryptionEnabled => encryptionEnabled;
        public string FolderName => ResolveFolderName();
        public string FileExtension => fileExtension;
        public string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, ResolveFolderName());

        public LocalFileSaveStorage CreateStorage() =>
            new(SaveDirectory, fileExtension);

        public SaveManager CreateManager(ISaveSerializer serializer = null)
        {
            ISaveSerializer resolvedSerializer = serializer ?? JsonSaveSerializer.Instance;
            LocalFileSaveStorage storage = CreateStorage();

            if (!encryptionEnabled)
                return new SaveManager(resolvedSerializer, storage);

            if (string.IsNullOrWhiteSpace(encryptionPassword))
            {
                throw new InvalidOperationException(
                    "Encryption Password is required when encryption is enabled.");
            }

            return new SaveManager(
                resolvedSerializer,
                storage,
                new AesSaveEncryption(
                    new PasswordSaveKeyProvider(encryptionPassword)));
        }

        private string ResolveFolderName()
        {
            string value = string.IsNullOrWhiteSpace(folderName)
                ? DefaultFolderName
                : folderName.Trim();

            if (!SaveSlotId.TryNormalize(value, out string normalizedFolderName))
            {
                throw new InvalidOperationException(
                    "Folder Name contains invalid path characters.");
            }

            return normalizedFolderName;
        }
    }
}