using System;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    internal static class SaveFileFormat
    {
        public const byte CurrentVersion = 1;
        public const int HeaderSize = 5;

        private static readonly byte[] Magic = { (byte)'P', (byte)'J', (byte)'D', (byte)'S' };

        public static byte[] Pack(byte[] payload, bool encrypted)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            byte flags = encrypted ? (byte)1 : (byte)0;
            var packed = new byte[HeaderSize + payload.Length];
            Buffer.BlockCopy(Magic, 0, packed, 0, Magic.Length);
            packed[4] = (byte)((CurrentVersion << 1) | (flags & 1));
            Buffer.BlockCopy(payload, 0, packed, HeaderSize, payload.Length);
            return packed;
        }

        public static bool TryUnpack(byte[] fileData, out byte[] payload, out bool encrypted)
        {
            payload = null;
            encrypted = false;

            if (fileData == null || fileData.Length <= HeaderSize)
                return false;

            for (int i = 0; i < Magic.Length; i++)
            {
                if (fileData[i] != Magic[i])
                    return false;
            }

            byte header = fileData[4];
            int version = header >> 1;
            if (version != CurrentVersion)
                return false;

            encrypted = (header & 1) == 1;
            payload = new byte[fileData.Length - HeaderSize];
            Buffer.BlockCopy(fileData, HeaderSize, payload, 0, payload.Length);
            return true;
        }
    }
}
