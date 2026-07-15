using System;
using System.Security.Cryptography;
using System.Text;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed class PasswordSaveKeyProvider : ISaveKeyProvider
    {
        private const int DefaultIterations = 100000;

        private static readonly byte[] DefaultSalt =
            Encoding.UTF8.GetBytes("PJDev.DevelopKit.SaveSystem.v2");

        private readonly byte[] key;

        public PasswordSaveKeyProvider(
            string password,
            byte[] salt = null,
            int iterations = DefaultIterations)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("An encryption password is required.", nameof(password));

            if (iterations < 10000)
                throw new ArgumentOutOfRangeException(nameof(iterations), "Use at least 10,000 iterations.");

            byte[] resolvedSalt = salt ?? DefaultSalt;
            using var derive = new Rfc2898DeriveBytes(password, resolvedSalt, iterations);
            key = derive.GetBytes(32);
        }

        public byte[] GetKey()
        {
            var copy = new byte[key.Length];
            Buffer.BlockCopy(key, 0, copy, 0, key.Length);
            return copy;
        }
    }
}