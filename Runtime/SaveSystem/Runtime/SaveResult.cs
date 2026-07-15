namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public readonly struct SaveResult
    {
        private SaveResult(SaveError error, string slotId, string message)
        {
            Error = error;
            SlotId = slotId;
            Message = message;
        }

        public bool IsSuccess => Error == SaveError.None;
        public SaveError Error { get; }
        public string SlotId { get; }
        public string Message { get; }

        public static SaveResult Success(string slotId) =>
            new(SaveError.None, slotId, null);

        public static SaveResult Failure(
            SaveError error,
            string slotId = null,
            string message = null) =>
            new(error, slotId, message);
    }

    public readonly struct LoadResult<T>
    {
        private LoadResult(SaveError error, string slotId, T value, string message)
        {
            Error = error;
            SlotId = slotId;
            Value = value;
            Message = message;
        }

        public bool IsSuccess => Error == SaveError.None;
        public SaveError Error { get; }
        public string SlotId { get; }
        public T Value { get; }
        public string Message { get; }

        public static LoadResult<T> Success(string slotId, T value) =>
            new(SaveError.None, slotId, value, null);

        public static LoadResult<T> Failure(
            SaveError error,
            string slotId = null,
            string message = null) =>
            new(error, slotId, default, message);
    }
}