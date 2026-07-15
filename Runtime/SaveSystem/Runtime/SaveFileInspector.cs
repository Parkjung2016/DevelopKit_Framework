namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public readonly struct SaveFileMetadata
    {
        internal SaveFileMetadata(
            int version,
            bool isEncrypted,
            int payloadSize,
            uint checksum)
        {
            Version = version;
            IsEncrypted = isEncrypted;
            PayloadSize = payloadSize;
            Checksum = checksum;
        }

        public int Version { get; }
        public bool IsEncrypted { get; }
        public int PayloadSize { get; }
        public uint Checksum { get; }
    }

    public static class SaveFileInspector
    {
        public static bool TryInspect(byte[] fileBytes, out SaveFileMetadata metadata) =>
            SaveFileEnvelope.TryInspect(fileBytes, out metadata);
    }
}