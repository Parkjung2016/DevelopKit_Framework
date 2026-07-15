using System;
using System.Security.Cryptography;
using System.Text;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    /// <summary>AES-256-CBC로 암호화하고 HMAC-SHA256으로 변조를 검증합니다.</summary>
    public sealed class AesSaveEncryption : ISaveEncryption
    {
        private const int IvSize = 16;
        private const int TagSize = 32;

        private static readonly byte[] KeyContext =
            Encoding.UTF8.GetBytes("PJDev.DevelopKit.SaveSystem.Aes.v2");

        private readonly byte[] encryptionKey;
        private readonly byte[] authenticationKey;

        public AesSaveEncryption(ISaveKeyProvider keyProvider)
        {
            if (keyProvider == null)
                throw new ArgumentNullException(nameof(keyProvider));

            byte[] masterKey = keyProvider.GetKey();
            if (masterKey == null || masterKey.Length < 16)
                throw new InvalidOperationException("The save key must contain at least 16 bytes.");

            using var keyDerivation = new HMACSHA512(masterKey);
            byte[] derivedKey = keyDerivation.ComputeHash(KeyContext);
            Array.Clear(masterKey, 0, masterKey.Length);
            encryptionKey = CopyRange(derivedKey, 0, 32);
            authenticationKey = CopyRange(derivedKey, 32, 32);
            Array.Clear(derivedKey, 0, derivedKey.Length);
        }

        public bool IsEnabled => true;

        public byte[] Encrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data is empty.", nameof(data));

            using Aes aes = CreateAes();
            aes.GenerateIV();

            byte[] cipher;
            using (ICryptoTransform transform = aes.CreateEncryptor())
                cipher = transform.TransformFinalBlock(data, 0, data.Length);

            int signedLength = IvSize + cipher.Length;
            var encrypted = new byte[signedLength + TagSize];
            Buffer.BlockCopy(aes.IV, 0, encrypted, 0, IvSize);
            Buffer.BlockCopy(cipher, 0, encrypted, IvSize, cipher.Length);

            using var hmac = new HMACSHA256(authenticationKey);
            byte[] tag = hmac.ComputeHash(encrypted, 0, signedLength);
            Buffer.BlockCopy(tag, 0, encrypted, signedLength, TagSize);
            return encrypted;
        }

        public byte[] Decrypt(byte[] data)
        {
            if (data == null || data.Length <= IvSize + TagSize)
                throw new CryptographicException("Encrypted save data is too short.");

            int signedLength = data.Length - TagSize;
            using (var hmac = new HMACSHA256(authenticationKey))
            {
                byte[] expectedTag = hmac.ComputeHash(data, 0, signedLength);
                if (!TagsMatch(expectedTag, data, signedLength))
                    throw new CryptographicException("Save data authentication failed.");
            }

            using Aes aes = CreateAes();
            var iv = new byte[IvSize];
            Buffer.BlockCopy(data, 0, iv, 0, IvSize);
            aes.IV = iv;

            int cipherLength = signedLength - IvSize;
            using ICryptoTransform transform = aes.CreateDecryptor();
            return transform.TransformFinalBlock(data, IvSize, cipherLength);
        }

        private Aes CreateAes()
        {
            Aes aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        private static bool TagsMatch(byte[] expected, byte[] data, int tagOffset)
        {
            int difference = 0;
            for (int i = 0; i < TagSize; i++)
                difference |= expected[i] ^ data[tagOffset + i];

            return difference == 0;
        }

        private static byte[] CopyRange(byte[] source, int offset, int count)
        {
            var copy = new byte[count];
            Buffer.BlockCopy(source, offset, copy, 0, count);
            return copy;
        }
    }
}