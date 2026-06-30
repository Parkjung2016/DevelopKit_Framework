using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    /// <summary>직렬화 + (선택) 암호화 + 스토리지를 묶은 범용 로컬 세이브 API입니다.</summary>
    public sealed class SaveManager
    {
        private readonly ISaveSerializer serializer;
        private readonly ISaveEncryptor encryptor;
        private readonly ISaveStorage storage;
        private readonly bool encryptedPayload;

        public SaveManager(
            ISaveSerializer serializer,
            ISaveEncryptor encryptor,
            ISaveStorage storage,
            bool encryptedPayload = true)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.encryptedPayload = encryptedPayload;
        }

        public ISaveSerializer Serializer => serializer;
        public ISaveEncryptor Encryptor => encryptor;
        public ISaveStorage Storage => storage;

        public static SaveManager CreateDefault(string passphrase = null, string rootDirectory = null)
        {
            string resolvedPassphrase = string.IsNullOrWhiteSpace(passphrase)
                ? Application.productName
                : passphrase;

            return new SaveManager(
                JsonSaveSerializer.Instance,
                new AesSaveEncryptor(new PassphraseSaveKeyProvider(resolvedPassphrase)),
                new LocalFileSaveStorage(rootDirectory));
        }

        public static SaveManager CreateUnencrypted(string rootDirectory = null) =>
            new(
                JsonSaveSerializer.Instance,
                NullSaveEncryptor.Instance,
                new LocalFileSaveStorage(rootDirectory),
                encryptedPayload: false);

        public bool Exists(string slotId) =>
            SaveSlotId.TryNormalize(slotId, out string normalized) && storage.Exists(normalized);

        public SaveResult Save<T>(string slotId, T data)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return SaveResult.Fail(SaveFailReason.InvalidSlotId, slotId);

            try
            {
                byte[] plain = serializer.Serialize(data);
                byte[] payload = encryptedPayload ? encryptor.Encrypt(plain) : plain;
                byte[] fileBytes = SaveFileFormat.Pack(payload, encryptedPayload);

                return storage.TryWrite(normalized, fileBytes)
                    ? SaveResult.Succeed(normalized)
                    : SaveResult.Fail(SaveFailReason.WriteFailed, normalized);
            }
            catch (Exception exception)
            {
                CDebug.LogWarning($"SaveManager : failed to save slot '{normalized}'. {exception.Message}");
                return SaveResult.Fail(SaveFailReason.SerializationFailed, normalized);
            }
        }

        public SaveResult SaveRaw(string slotId, byte[] plainBytes)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return SaveResult.Fail(SaveFailReason.InvalidSlotId, slotId);

            if (plainBytes == null || plainBytes.Length == 0)
                return SaveResult.Fail(SaveFailReason.SerializationFailed, normalized);

            byte[] payload = encryptedPayload ? encryptor.Encrypt(plainBytes) : plainBytes;
            byte[] fileBytes = SaveFileFormat.Pack(payload, encryptedPayload);

            return storage.TryWrite(normalized, fileBytes)
                ? SaveResult.Succeed(normalized)
                : SaveResult.Fail(SaveFailReason.WriteFailed, normalized);
        }

        public SaveLoadResult<T> TryLoad<T>(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return SaveLoadResult<T>.Fail(SaveFailReason.InvalidSlotId, slotId);

            if (!storage.TryRead(normalized, out byte[] fileBytes))
                return SaveLoadResult<T>.Fail(SaveFailReason.SlotNotFound, normalized);

            if (!SaveFileFormat.TryUnpack(fileBytes, out byte[] payload, out bool encrypted))
                return SaveLoadResult<T>.Fail(SaveFailReason.InvalidFormat, normalized);

            if (encrypted)
            {
                if (!encryptor.TryDecrypt(payload, out byte[] plain))
                    return SaveLoadResult<T>.Fail(SaveFailReason.DecryptionFailed, normalized);

                payload = plain;
            }

            return serializer.TryDeserialize(payload, out T value)
                ? SaveLoadResult<T>.Succeed(normalized, value)
                : SaveLoadResult<T>.Fail(SaveFailReason.DeserializationFailed, normalized);
        }

        public SaveLoadResult<byte[]> TryLoadRaw(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return SaveLoadResult<byte[]>.Fail(SaveFailReason.InvalidSlotId, slotId);

            if (!storage.TryRead(normalized, out byte[] fileBytes))
                return SaveLoadResult<byte[]>.Fail(SaveFailReason.SlotNotFound, normalized);

            if (!SaveFileFormat.TryUnpack(fileBytes, out byte[] payload, out bool encrypted))
                return SaveLoadResult<byte[]>.Fail(SaveFailReason.InvalidFormat, normalized);

            if (!encrypted)
                return SaveLoadResult<byte[]>.Succeed(normalized, payload);

            return encryptor.TryDecrypt(payload, out byte[] plain)
                ? SaveLoadResult<byte[]>.Succeed(normalized, plain)
                : SaveLoadResult<byte[]>.Fail(SaveFailReason.DecryptionFailed, normalized);
        }

        public SaveResult Delete(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return SaveResult.Fail(SaveFailReason.InvalidSlotId, slotId);

            return storage.TryDelete(normalized)
                ? SaveResult.Succeed(normalized)
                : SaveResult.Fail(SaveFailReason.SlotNotFound, normalized);
        }
    }
}
