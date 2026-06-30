using System;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.SaveSystem.Runtime;

namespace PJDev.DevelopKit.Framework.SaveSystem.Tests
{
    [Serializable]
    public sealed class TestSavePayload
    {
        public int Level;
        public string PlayerName;
        public float Health;
    }

    [TestFixture]
    public sealed class AesSaveEncryptorTests
    {
        [Test]
        public void EncryptDecrypt_RoundTripsPlainText()
        {
            var encryptor = new AesSaveEncryptor(new PassphraseSaveKeyProvider("test-passphrase"));
            byte[] plain = { 1, 2, 3, 4, 5, 6, 7, 8 };

            byte[] cipher = encryptor.Encrypt(plain);

            Assert.Greater(cipher.Length, plain.Length);
            Assert.IsTrue(encryptor.TryDecrypt(cipher, out byte[] decrypted));
            CollectionAssert.AreEqual(plain, decrypted);
        }

        [Test]
        public void TryDecrypt_WithWrongKey_ReturnsFalse()
        {
            var encryptorA = new AesSaveEncryptor(new PassphraseSaveKeyProvider("key-a"));
            var encryptorB = new AesSaveEncryptor(new PassphraseSaveKeyProvider("key-b"));
            byte[] cipher = encryptorA.Encrypt(new byte[] { 9, 8, 7 });

            Assert.IsFalse(encryptorB.TryDecrypt(cipher, out _));
        }
    }

    [TestFixture]
    public sealed class SaveSystemTests
    {
        private InMemorySaveStorage storage;
        private SaveSystem saveSystem;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemorySaveStorage();
            saveSystem = new SaveSystem(
                JsonSaveSerializer.Instance,
                new AesSaveEncryptor(new PassphraseSaveKeyProvider("unit-test")),
                storage);
        }

        [Test]
        public void SaveAndLoad_RoundTripsSerializableObject()
        {
            var payload = new TestSavePayload
            {
                Level = 12,
                PlayerName = "Hero",
                Health = 87.5f
            };

            SaveResult saveResult = saveSystem.Save("slot-1", payload);
            SaveLoadResult<TestSavePayload> loadResult = saveSystem.TryLoad<TestSavePayload>("slot-1");

            Assert.IsTrue(saveResult.Success);
            Assert.IsTrue(loadResult.Success);
            Assert.AreEqual(12, loadResult.Value.Level);
            Assert.AreEqual("Hero", loadResult.Value.PlayerName);
            Assert.AreEqual(87.5f, loadResult.Value.Health);
        }

        [Test]
        public void SaveRawAndLoadRaw_RoundTripsBytes()
        {
            byte[] plain = { 10, 20, 30 };

            Assert.IsTrue(saveSystem.SaveRaw("raw-slot", plain).Success);
            SaveLoadResult<byte[]> loadResult = saveSystem.TryLoadRaw("raw-slot");

            Assert.IsTrue(loadResult.Success);
            CollectionAssert.AreEqual(plain, loadResult.Value);
        }

        [Test]
        public void TryLoad_MissingSlot_ReturnsSlotNotFound()
        {
            SaveLoadResult<TestSavePayload> loadResult = saveSystem.TryLoad<TestSavePayload>("missing");

            Assert.IsFalse(loadResult.Success);
            Assert.AreEqual(SaveFailReason.SlotNotFound, loadResult.Reason);
        }

        [Test]
        public void Delete_RemovesSlot()
        {
            saveSystem.Save("delete-me", new TestSavePayload { Level = 1 });
            Assert.IsTrue(saveSystem.Exists("delete-me"));

            SaveResult deleteResult = saveSystem.Delete("delete-me");

            Assert.IsTrue(deleteResult.Success);
            Assert.IsFalse(saveSystem.Exists("delete-me"));
        }

        [Test]
        public void Save_InvalidSlotId_ReturnsInvalidSlotId()
        {
            SaveResult result = saveSystem.Save("", new TestSavePayload());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveFailReason.InvalidSlotId, result.Reason);
        }

        [Test]
        public void UnencryptedSaveSystem_StillUsesFileHeader()
        {
            var openSave = new SaveSystem(
                JsonSaveSerializer.Instance,
                NullSaveEncryptor.Instance,
                storage,
                encryptedPayload: false);

            openSave.Save("open", new TestSavePayload { Level = 3 });
            SaveLoadResult<TestSavePayload> loadResult = openSave.TryLoad<TestSavePayload>("open");

            Assert.IsTrue(loadResult.Success);
            Assert.AreEqual(3, loadResult.Value.Level);
        }
    }
}
