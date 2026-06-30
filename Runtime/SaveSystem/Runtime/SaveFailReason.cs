namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public enum SaveFailReason
    {
        None = 0,
        InvalidSlotId = 1,
        SlotNotFound = 2,
        WriteFailed = 3,
        ReadFailed = 4,
        InvalidFormat = 5,
        DecryptionFailed = 6,
        DeserializationFailed = 7,
        SerializationFailed = 8
    }
}
