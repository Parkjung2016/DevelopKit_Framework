using System;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    /// <summary>직렬화, 암호화, 저장소를 하나의 슬롯 기반 API로 제공합니다.</summary>
    public sealed partial class SaveManager
    {
        private readonly ISaveSerializer serializer;
        private readonly ISaveStorage storage;
        private readonly ISaveEncryption encryption;

        public SaveManager(
            ISaveSerializer serializer,
            ISaveStorage storage,
            ISaveEncryption encryption = null)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.encryption = encryption ?? NoSaveEncryption.Instance;
        }

        public ISaveSerializer Serializer => serializer;
        public ISaveStorage Storage => storage;
        public ISaveEncryption Encryption => encryption;

        public static SaveManager CreateEncrypted(
            string encryptionPassword,
            string saveDirectory = null,
            string fileExtension = ".sav")
        {
            if (string.IsNullOrWhiteSpace(encryptionPassword))
                throw new ArgumentException("An encryption password is required.", nameof(encryptionPassword));

            return new SaveManager(
                JsonSaveSerializer.Instance,
                new LocalFileSaveStorage(saveDirectory, fileExtension),
                new AesSaveEncryption(new PasswordSaveKeyProvider(encryptionPassword)));
        }

        public static SaveManager CreateUnencrypted(
            string saveDirectory = null,
            string fileExtension = ".sav") =>
            new(
                JsonSaveSerializer.Instance,
                new LocalFileSaveStorage(saveDirectory, fileExtension));

        public bool HasSlot(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
                return false;

            try
            {
                return storage.Exists(normalizedSlotId);
            }
            catch
            {
                return false;
            }
        }

        public SaveResult Save<T>(string slotId, T data)
        {
            SaveResult prepareResult = PrepareSave(slotId, data, out string normalizedSlotId, out byte[] fileBytes);
            if (!prepareResult.IsSuccess)
                return prepareResult;

            return Write(normalizedSlotId, fileBytes);
        }

        public SaveResult SaveBytes(string slotId, byte[] bytes)
        {
            SaveResult prepareResult = PrepareBytes(slotId, bytes, out string normalizedSlotId, out byte[] fileBytes);
            if (!prepareResult.IsSuccess)
                return prepareResult;

            return Write(normalizedSlotId, fileBytes);
        }

        public LoadResult<T> Load<T>(string slotId)
        {
            LoadResult<byte[]> bytesResult = LoadBytes(slotId);
            return bytesResult.IsSuccess
                ? Deserialize<T>(bytesResult)
                : CopyFailure<T>(bytesResult);
        }

        public LoadResult<byte[]> LoadBytes(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
                return InvalidSlotLoadResult<byte[]>(slotId);

            SaveStorageReadResult readResult;
            try
            {
                readResult = storage.Read(normalizedSlotId);
            }
            catch (Exception exception)
            {
                return LoadResult<byte[]>.Failure(
                    SaveError.ReadFailed,
                    normalizedSlotId,
                    exception.Message);
            }

            return DecodeReadResult(normalizedSlotId, readResult);
        }

        public SaveResult Delete(string slotId)
        {
            if (!TryGetSlotId(slotId, out string normalizedSlotId, out SaveResult failure))
                return failure;

            try
            {
                return storage.Delete(normalizedSlotId)
                    ? SaveResult.Success(normalizedSlotId)
                    : SaveResult.Failure(SaveError.SlotNotFound, normalizedSlotId);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveError.DeleteFailed,
                    normalizedSlotId,
                    exception.Message);
            }
        }

        private SaveResult PrepareSave<T>(
            string slotId,
            T data,
            out string normalizedSlotId,
            out byte[] fileBytes)
        {
            fileBytes = null;
            if (!TryGetSlotId(slotId, out normalizedSlotId, out SaveResult failure))
                return failure;

            byte[] bytes;
            try
            {
                bytes = serializer.Serialize(data);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveError.SerializationFailed,
                    normalizedSlotId,
                    exception.Message);
            }

            return CreateFileBytes(normalizedSlotId, bytes, out fileBytes);
        }

        private SaveResult PrepareBytes(
            string slotId,
            byte[] bytes,
            out string normalizedSlotId,
            out byte[] fileBytes)
        {
            fileBytes = null;
            if (!TryGetSlotId(slotId, out normalizedSlotId, out SaveResult failure))
                return failure;

            return CreateFileBytes(normalizedSlotId, bytes, out fileBytes);
        }

        private SaveResult CreateFileBytes(
            string normalizedSlotId,
            byte[] bytes,
            out byte[] fileBytes)
        {
            fileBytes = null;
            if (bytes == null || bytes.Length == 0)
            {
                return SaveResult.Failure(
                    SaveError.InvalidData,
                    normalizedSlotId,
                    "Save data is empty.");
            }

            byte[] payload;
            try
            {
                payload = encryption.IsEnabled ? encryption.Encrypt(bytes) : bytes;
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveError.EncryptionFailed,
                    normalizedSlotId,
                    exception.Message);
            }

            fileBytes = SaveFileEnvelope.Pack(payload, encryption.IsEnabled);
            return SaveResult.Success(normalizedSlotId);
        }

        private SaveResult Write(string normalizedSlotId, byte[] fileBytes)
        {
            try
            {
                return storage.Write(normalizedSlotId, fileBytes)
                    ? SaveResult.Success(normalizedSlotId)
                    : SaveResult.Failure(SaveError.WriteFailed, normalizedSlotId);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveError.WriteFailed,
                    normalizedSlotId,
                    exception.Message);
            }
        }

        private LoadResult<byte[]> DecodeReadResult(
            string normalizedSlotId,
            SaveStorageReadResult readResult)
        {
            if (!readResult.IsSuccess)
            {
                SaveError error = readResult.Status == SaveStorageReadStatus.NotFound
                    ? SaveError.SlotNotFound
                    : SaveError.ReadFailed;

                return LoadResult<byte[]>.Failure(error, normalizedSlotId, readResult.Message);
            }

            if (!SaveFileEnvelope.TryUnpack(readResult.Data, out SaveFileContent content))
            {
                return LoadResult<byte[]>.Failure(
                    SaveError.InvalidFile,
                    normalizedSlotId,
                    "The save file header, length, or checksum is invalid.");
            }

            if (!content.IsEncrypted)
                return LoadResult<byte[]>.Success(normalizedSlotId, content.Data);

            if (!encryption.IsEnabled)
            {
                return LoadResult<byte[]>.Failure(
                    SaveError.DecryptionFailed,
                    normalizedSlotId,
                    "This save is encrypted, but no encryption implementation is configured.");
            }

            try
            {
                return LoadResult<byte[]>.Success(
                    normalizedSlotId,
                    encryption.Decrypt(content.Data));
            }
            catch (Exception exception)
            {
                return LoadResult<byte[]>.Failure(
                    SaveError.DecryptionFailed,
                    normalizedSlotId,
                    exception.Message);
            }
        }

        private LoadResult<T> Deserialize<T>(LoadResult<byte[]> bytesResult)
        {
            try
            {
                return LoadResult<T>.Success(
                    bytesResult.SlotId,
                    serializer.Deserialize<T>(bytesResult.Value));
            }
            catch (Exception exception)
            {
                return LoadResult<T>.Failure(
                    SaveError.DeserializationFailed,
                    bytesResult.SlotId,
                    exception.Message);
            }
        }

        private static LoadResult<T> CopyFailure<T>(LoadResult<byte[]> result) =>
            LoadResult<T>.Failure(result.Error, result.SlotId, result.Message);

        private static LoadResult<T> InvalidSlotLoadResult<T>(string slotId) =>
            LoadResult<T>.Failure(
                SaveError.InvalidSlotId,
                slotId,
                "Slot ID is empty or contains invalid characters.");

        private static bool TryGetSlotId(
            string slotId,
            out string normalizedSlotId,
            out SaveResult failure)
        {
            if (SaveSlotId.TryNormalize(slotId, out normalizedSlotId))
            {
                failure = default;
                return true;
            }

            failure = SaveResult.Failure(
                SaveError.InvalidSlotId,
                slotId,
                "Slot ID is empty or contains invalid characters.");
            return false;
        }
    }
}