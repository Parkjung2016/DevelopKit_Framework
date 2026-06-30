namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public readonly struct SaveResult
    {
        public bool Success => Reason == SaveFailReason.None;
        public SaveFailReason Reason { get; }
        public string SlotId { get; }

        public SaveResult(SaveFailReason reason, string slotId = null)
        {
            Reason = reason;
            SlotId = slotId;
        }

        public static SaveResult Succeed(string slotId) => new(SaveFailReason.None, slotId);

        public static SaveResult Fail(SaveFailReason reason, string slotId = null) => new(reason, slotId);
    }

    public readonly struct SaveLoadResult<T>
    {
        public bool Success => Reason == SaveFailReason.None;
        public SaveFailReason Reason { get; }
        public string SlotId { get; }
        public T Value { get; }

        public SaveLoadResult(SaveFailReason reason, string slotId = null, T value = default)
        {
            Reason = reason;
            SlotId = slotId;
            Value = value;
        }

        public static SaveLoadResult<T> Succeed(string slotId, T value) =>
            new(SaveFailReason.None, slotId, value);

        public static SaveLoadResult<T> Fail(SaveFailReason reason, string slotId = null) =>
            new(reason, slotId);
    }
}
