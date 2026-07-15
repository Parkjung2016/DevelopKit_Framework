namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public enum SaveError
    {
        None = 0,
        InvalidSlotId = 1,
        SlotNotFound = 2,
        InvalidData = 3,
        WriteFailed = 4,
        ReadFailed = 5,
        DeleteFailed = 6,
        InvalidFile = 7,
        EncryptionFailed = 8,
        DecryptionFailed = 9,
        SerializationFailed = 10,
        DeserializationFailed = 11
    }
}