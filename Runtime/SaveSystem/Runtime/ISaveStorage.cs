namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public enum SaveStorageReadStatus
    {
        Success = 0,
        NotFound = 1,
        Failed = 2
    }

    public readonly struct SaveStorageReadResult
    {
        private SaveStorageReadResult(
            SaveStorageReadStatus status,
            byte[] data,
            string message)
        {
            Status = status;
            Data = data;
            Message = message;
        }

        public bool IsSuccess => Status == SaveStorageReadStatus.Success;
        public SaveStorageReadStatus Status { get; }
        public byte[] Data { get; }
        public string Message { get; }

        public static SaveStorageReadResult Success(byte[] data) =>
            new(SaveStorageReadStatus.Success, data, null);

        public static SaveStorageReadResult NotFound() =>
            new(SaveStorageReadStatus.NotFound, null, null);

        public static SaveStorageReadResult Failed(string message = null) =>
            new(SaveStorageReadStatus.Failed, null, message);
    }

    public interface ISaveStorage
    {
        bool Exists(string slotId);

        SaveStorageReadResult Read(string slotId);

        bool Write(string slotId, byte[] data);

        bool Delete(string slotId);
    }
}