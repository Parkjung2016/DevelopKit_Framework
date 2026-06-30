using System;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.SaveSystem.Runtime;

namespace PJDev.DevelopKit.Framework.SaveSystemTests
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
    public sealed class SaveManagerTests
    {
        private InMemorySaveStorage storage;
        private SaveManager saveManager;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemorySaveStorage();
            saveManager = new SaveManager(
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

            SaveResult saveResult = saveManager.Save("slot-1", payload);
            SaveLoadResult<TestSavePayload> loadResult = saveManager.TryLoad<TestSavePayload>("slot-1");

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

            Assert.IsTrue(saveManager.SaveRaw("raw-slot", plain).Success);
            SaveLoadResult<byte[]> loadResult = saveManager.TryLoadRaw("raw-slot");

            Assert.IsTrue(loadResult.Success);
            CollectionAssert.AreEqual(plain, loadResult.Value);
        }

        [Test]
        public void TryLoad_MissingSlot_ReturnsSlotNotFound()
        {
            SaveLoadResult<TestSavePayload> loadResult = saveManager.TryLoad<TestSavePayload>("missing");

            Assert.IsFalse(loadResult.Success);
            Assert.AreEqual(SaveFailReason.SlotNotFound, loadResult.Reason);
        }

        [Test]
        public void Delete_RemovesSlot()
        {
            saveManager.Save("delete-me", new TestSavePayload { Level = 1 });
            Assert.IsTrue(saveManager.Exists("delete-me"));

            SaveResult deleteResult = saveManager.Delete("delete-me");

            Assert.IsTrue(deleteResult.Success);
            Assert.IsFalse(saveManager.Exists("delete-me"));
        }

        [Test]
        public void Save_InvalidSlotId_ReturnsInvalidSlotId()
        {
            SaveResult result = saveManager.Save("", new TestSavePayload());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveFailReason.InvalidSlotId, result.Reason);
        }

        [Test]
        public void UnencryptedSaveManager_StillUsesFileHeader()
        {
            var openSave = new SaveManager(
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
