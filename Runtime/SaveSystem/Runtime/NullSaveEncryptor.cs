using System;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed class NullSaveEncryptor : ISaveEncryptor
    {
        public static readonly NullSaveEncryptor Instance = new();

        public byte[] Encrypt(byte[] plain)
        {
            if (plain == null)
                throw new ArgumentNullException(nameof(plain));

            var copy = new byte[plain.Length];
            Buffer.BlockCopy(plain, 0, copy, 0, plain.Length);
            return copy;
        }

        public bool TryDecrypt(byte[] cipher, out byte[] plain)
        {
            if (cipher == null || cipher.Length == 0)
            {
                plain = null;
                return false;
            }

            plain = Encrypt(cipher);
            return true;
        }
    }
}
