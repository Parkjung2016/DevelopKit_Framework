using System;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed class NoSaveEncryption : ISaveEncryption
    {
        public static readonly NoSaveEncryption Instance = new();

        private NoSaveEncryption()
        {
        }

        public bool IsEnabled => false;

        public byte[] Encrypt(byte[] data) =>
            throw new InvalidOperationException("Encryption is disabled.");

        public byte[] Decrypt(byte[] data) =>
            throw new InvalidOperationException("Encryption is disabled.");
    }
}