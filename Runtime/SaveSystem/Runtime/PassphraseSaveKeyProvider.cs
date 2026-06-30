using System;
using System.Security.Cryptography;
using System.Text;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed class PassphraseSaveKeyProvider : ISaveKeyProvider
    {
        private const int DefaultIterations = 10000;
        private static readonly byte[] DefaultSalt = Encoding.UTF8.GetBytes("PJDev.DevelopKit.SaveSystem.v1");

        private readonly byte[] key;

        public PassphraseSaveKeyProvider(string passphrase, byte[] salt = null, int iterations = DefaultIterations)
        {
            if (string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("Passphrase is required.", nameof(passphrase));

            byte[] resolvedSalt = salt ?? DefaultSalt;
            using var derive = new Rfc2898DeriveBytes(passphrase, resolvedSalt, iterations);
            key = derive.GetBytes(32);
        }

        public byte[] GetKey() => key;
    }
}
