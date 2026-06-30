using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_SaveSetup", menuName = "PJDev/SO/SaveSystem/Setup")]
    public sealed class SaveSetupSO : ScriptableObject
    {
        [field: SerializeField] public string Passphrase { get; set; } = "change-me";
        [field: SerializeField] public bool UseEncryption { get; set; } = true;
        [field: SerializeField] public string SaveRootDirectory { get; set; } = "";
        [field: SerializeField] public string FileExtension { get; set; } = ".sav";

        public SaveSystem CreateSaveSystem()
        {
            ISaveEncryptor encryptor = UseEncryption
                ? new AesSaveEncryptor(new PassphraseSaveKeyProvider(ResolvePassphrase()))
                : NullSaveEncryptor.Instance;

            string root = string.IsNullOrWhiteSpace(SaveRootDirectory) ? null : SaveRootDirectory;
            var storage = new LocalFileSaveStorage(root, FileExtension);
            return new SaveSystem(JsonSaveSerializer.Instance, encryptor, storage);
        }

        private string ResolvePassphrase()
        {
            if (!string.IsNullOrWhiteSpace(Passphrase))
                return Passphrase;

            return Application.productName;
        }
    }
}
