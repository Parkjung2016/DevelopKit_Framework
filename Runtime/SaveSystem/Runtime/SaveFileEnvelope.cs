using System;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    internal readonly struct SaveFileContent
    {
        public SaveFileContent(byte[] data, bool isEncrypted)
        {
            Data = data;
            IsEncrypted = isEncrypted;
        }

        public byte[] Data { get; }
        public bool IsEncrypted { get; }
    }

    internal static class SaveFileEnvelope
    {
        internal const byte CurrentVersion = 2;
        internal const int HeaderSize = 14;

        private const byte EncryptedFlag = 1;

        private static readonly byte[] Magic = { (byte)'P', (byte)'J', (byte)'D', (byte)'S' };
        private static readonly uint[] CrcTable = CreateCrcTable();

        public static byte[] Pack(byte[] payload, bool isEncrypted)
        {
            if (payload == null || payload.Length == 0)
                throw new ArgumentException("Payload is empty.", nameof(payload));

            var fileBytes = new byte[HeaderSize + payload.Length];
            Buffer.BlockCopy(Magic, 0, fileBytes, 0, Magic.Length);
            fileBytes[4] = CurrentVersion;
            fileBytes[5] = isEncrypted ? EncryptedFlag : (byte)0;
            WriteInt32(fileBytes, 6, payload.Length);
            WriteUInt32(fileBytes, 10, ComputeCrc(payload, 0, payload.Length));
            Buffer.BlockCopy(payload, 0, fileBytes, HeaderSize, payload.Length);
            return fileBytes;
        }

        public static bool TryInspect(byte[] fileBytes, out SaveFileMetadata metadata)
        {
            metadata = default;
            if (fileBytes == null || fileBytes.Length <= HeaderSize)
                return false;

            for (int i = 0; i < Magic.Length; i++)
            {
                if (fileBytes[i] != Magic[i])
                    return false;
            }

            byte version = fileBytes[4];
            if (version != CurrentVersion)
                return false;

            byte flags = fileBytes[5];
            if ((flags & ~EncryptedFlag) != 0)
                return false;

            int payloadLength = ReadInt32(fileBytes, 6);
            if (payloadLength <= 0 || fileBytes.Length != HeaderSize + payloadLength)
                return false;

            uint checksum = ReadUInt32(fileBytes, 10);
            uint actualChecksum = ComputeCrc(fileBytes, HeaderSize, payloadLength);
            if (checksum != actualChecksum)
                return false;

            metadata = new SaveFileMetadata(
                version,
                (flags & EncryptedFlag) != 0,
                payloadLength,
                checksum);
            return true;
        }

        public static bool TryUnpack(byte[] fileBytes, out SaveFileContent content)
        {
            content = default;
            if (!TryInspect(fileBytes, out SaveFileMetadata metadata))
                return false;

            var payload = new byte[metadata.PayloadSize];
            Buffer.BlockCopy(fileBytes, HeaderSize, payload, 0, payload.Length);
            content = new SaveFileContent(payload, metadata.IsEncrypted);
            return true;
        }

        private static uint ComputeCrc(byte[] data, int offset, int count)
        {
            uint crc = uint.MaxValue;
            int end = offset + count;
            for (int i = offset; i < end; i++)
                crc = (crc >> 8) ^ CrcTable[(crc ^ data[i]) & 0xFF];

            return ~crc;
        }

        private static uint[] CreateCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;

                table[i] = value;
            }

            return table;
        }

        private static void WriteInt32(byte[] data, int offset, int value) =>
            WriteUInt32(data, offset, unchecked((uint)value));

        private static int ReadInt32(byte[] data, int offset) =>
            unchecked((int)ReadUInt32(data, offset));

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static uint ReadUInt32(byte[] data, int offset) =>
            data[offset]
            | ((uint)data[offset + 1] << 8)
            | ((uint)data[offset + 2] << 16)
            | ((uint)data[offset + 3] << 24);
    }
}