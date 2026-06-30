using System;
using System.Security.Cryptography;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    /// <summary>AES-256-CBC. 출력 = IV(16) + cipher.</summary>
    public sealed class AesSaveEncryptor : ISaveEncryptor
    {
        private const int IvSize = 16;

        private readonly byte[] key;

        public AesSaveEncryptor(ISaveKeyProvider keyProvider)
        {
            if (keyProvider == null)
                throw new ArgumentNullException(nameof(keyProvider));

            key = keyProvider.GetKey() ?? throw new InvalidOperationException("Save key provider returned null.");
            if (key.Length != 32)
                throw new InvalidOperationException("AES-256 requires a 32-byte key.");
        }

        public byte[] Encrypt(byte[] plain)
        {
            if (plain == null)
                throw new ArgumentNullException(nameof(plain));

            using Aes aes = CreateAes();
            aes.GenerateIV();
            byte[] iv = aes.IV;

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

            var packed = new byte[IvSize + cipher.Length];
            Buffer.BlockCopy(iv, 0, packed, 0, IvSize);
            Buffer.BlockCopy(cipher, 0, packed, IvSize, cipher.Length);
            return packed;
        }

        public bool TryDecrypt(byte[] cipher, out byte[] plain)
        {
            plain = null;
            if (cipher == null || cipher.Length <= IvSize)
                return false;

            try
            {
                using Aes aes = CreateAes();
                var iv = new byte[IvSize];
                Buffer.BlockCopy(cipher, 0, iv, 0, IvSize);
                aes.IV = iv;

                int cipherLength = cipher.Length - IvSize;
                using ICryptoTransform decryptor = aes.CreateDecryptor();
                plain = decryptor.TransformFinalBlock(cipher, IvSize, cipherLength);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private Aes CreateAes()
        {
            Aes aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }
    }
}
