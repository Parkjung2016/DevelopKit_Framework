using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.SaveSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystemTests
{
    [Serializable]
    public sealed class TestSaveData
    {
        public int Level;
        public string PlayerName;
        public float Health;
    }

    [TestFixture]
    public sealed class SaveSettingsTests
    {
        [Test]
        public void NewSettings_UsesPortableDefaultLocation()
        {
            var settings = ScriptableObject.CreateInstance<SaveSettingsSO>();
            try
            {
                Assert.IsFalse(settings.EncryptionEnabled);
                Assert.AreEqual("Saves", settings.FolderName);
                Assert.AreEqual(
                    Path.Combine(Application.persistentDataPath, "Saves"),
                    settings.SaveDirectory);

                LocalFileSaveStorage storage = settings.CreateStorage();
                Assert.AreEqual(settings.SaveDirectory, storage.SaveDirectory);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
    [TestFixture]
    public sealed class AesSaveEncryptionTests
    {
        [Test]
        public void EncryptAndDecrypt_ReturnOriginalData()
        {
            var encryption = new AesSaveEncryption(
                new PasswordSaveKeyProvider("test-passphrase", iterations: 10000));
            byte[] source = { 1, 2, 3, 4, 5, 6, 7, 8 };

            byte[] encrypted = encryption.Encrypt(source);
            byte[] decrypted = encryption.Decrypt(encrypted);

            Assert.Greater(encrypted.Length, source.Length);
            CollectionAssert.AreEqual(source, decrypted);
        }

        [Test]
        public void Decrypt_WithWrongKey_Throws()
        {
            var encryption = new AesSaveEncryption(
                new PasswordSaveKeyProvider("key-a", iterations: 10000));
            var wrongEncryption = new AesSaveEncryption(
                new PasswordSaveKeyProvider("key-b", iterations: 10000));

            byte[] encrypted = encryption.Encrypt(new byte[] { 9, 8, 7 });

            Assert.Throws<CryptographicException>(() => wrongEncryption.Decrypt(encrypted));
        }

        [Test]
        public void Decrypt_WhenDataWasChanged_Throws()
        {
            var encryption = new AesSaveEncryption(
                new PasswordSaveKeyProvider("test-passphrase", iterations: 10000));
            byte[] encrypted = encryption.Encrypt(new byte[] { 1, 2, 3 });
            encrypted[20] ^= 0x40;

            Assert.Throws<CryptographicException>(() => encryption.Decrypt(encrypted));
        }
    }

    [TestFixture]
    public sealed class SaveManagerTests
    {
        private InMemorySaveStorage storage;
        private SaveManager manager;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemorySaveStorage();
            manager = new SaveManager(
                JsonSaveSerializer.Instance,
                storage,
                new AesSaveEncryption(
                    new PasswordSaveKeyProvider("unit-test", iterations: 10000)));
        }

        [Test]
        public void SaveAndLoad_ReturnOriginalObject()
        {
            SaveResult saveResult = manager.Save("slot-1", CreateTestData());
            LoadResult<TestSaveData> loadResult = manager.Load<TestSaveData>("slot-1");

            Assert.IsTrue(saveResult.IsSuccess);
            AssertSaveData(loadResult);
        }

        [Test]
        public void SaveBytesAndLoadBytes_ReturnOriginalData()
        {
            byte[] source = { 10, 20, 30 };

            SaveResult saveResult = manager.SaveBytes("raw-slot", source);
            LoadResult<byte[]> loadResult = manager.LoadBytes("raw-slot");

            Assert.IsTrue(saveResult.IsSuccess);
            Assert.IsTrue(loadResult.IsSuccess);
            CollectionAssert.AreEqual(source, loadResult.Value);
        }

        [Test]
        public void FileInspector_ReturnsVersionAndEncryption()
        {
            Assert.IsTrue(manager.SaveBytes("metadata", new byte[] { 1, 2, 3 }).IsSuccess);
            SaveStorageReadResult stored = storage.Read("metadata");

            bool inspected = SaveFileInspector.TryInspect(
                stored.Data,
                out SaveFileMetadata metadata);

            Assert.IsTrue(inspected);
            Assert.AreEqual(2, metadata.Version);
            Assert.IsTrue(metadata.IsEncrypted);
            Assert.Greater(metadata.PayloadSize, 0);
        }
        [Test]
        public void Load_MissingSlot_ReturnsSlotNotFound()
        {
            LoadResult<TestSaveData> result = manager.Load<TestSaveData>("missing");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SaveError.SlotNotFound, result.Error);
        }

        [Test]
        public void Load_WhenFileWasChanged_ReturnsInvalidFile()
        {
            Assert.IsTrue(manager.SaveBytes("changed", new byte[] { 1, 2, 3 }).IsSuccess);
            SaveStorageReadResult stored = storage.Read("changed");
            Assert.IsTrue(stored.IsSuccess);

            byte[] fileBytes = stored.Data;
            fileBytes[fileBytes.Length - 1] ^= 0x10;
            Assert.IsTrue(storage.Write("changed", fileBytes));

            LoadResult<byte[]> result = manager.LoadBytes("changed");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SaveError.InvalidFile, result.Error);
        }

        [Test]
        public void Delete_RemovesSlot()
        {
            manager.Save("delete-me", new TestSaveData { Level = 1 });

            SaveResult result = manager.Delete("delete-me");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(manager.HasSlot("delete-me"));
        }

        [Test]
        public void Save_WithInvalidSlotId_ReturnsInvalidSlotId()
        {
            SaveResult result = manager.Save("folder/slot", new TestSaveData());

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SaveError.InvalidSlotId, result.Error);
        }

        [Test]
        public void SaveBytes_WhenStorageThrows_ReturnsWriteFailed()
        {
            var failingManager = new SaveManager(
                JsonSaveSerializer.Instance,
                new ThrowingSaveStorage());

            SaveResult result = failingManager.SaveBytes("slot", new byte[] { 1 });

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SaveError.WriteFailed, result.Error);
            Assert.IsNotEmpty(result.Message);
        }

        [Test]
        public void Load_WhenStorageReadFails_ReturnsReadFailed()
        {
            var failingManager = new SaveManager(
                JsonSaveSerializer.Instance,
                new UnreadableSaveStorage());

            LoadResult<TestSaveData> result = failingManager.Load<TestSaveData>("slot");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SaveError.ReadFailed, result.Error);
        }

        [Test]
        public void UnencryptedManager_CanSaveAndLoad()
        {
            var unencryptedManager = new SaveManager(
                JsonSaveSerializer.Instance,
                storage);

            Assert.IsTrue(unencryptedManager.Save("open", new TestSaveData { Level = 3 }).IsSuccess);
            LoadResult<TestSaveData> result = unencryptedManager.Load<TestSaveData>("open");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Value.Level);
        }

        [Test]
        public async Task SaveAsyncAndLoadAsync_ReturnOriginalObject()
        {
            SaveResult saveResult = await manager.SaveAsync("async-slot", CreateTestData());
            LoadResult<TestSaveData> loadResult =
                await manager.LoadAsync<TestSaveData>("async-slot");

            Assert.IsTrue(saveResult.IsSuccess);
            AssertSaveData(loadResult);
        }

        [Test]
        public void SaveAsync_WhenCancelled_Throws()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await manager.SaveAsync(
                    "cancelled",
                    CreateTestData(),
                    cancellation.Token));
        }

        private static TestSaveData CreateTestData() =>
            new()
            {
                Level = 12,
                PlayerName = "Hero",
                Health = 87.5f
            };

        private static void AssertSaveData(LoadResult<TestSaveData> result)
        {
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(12, result.Value.Level);
            Assert.AreEqual("Hero", result.Value.PlayerName);
            Assert.AreEqual(87.5f, result.Value.Health);
        }

        private sealed class ThrowingSaveStorage : ISaveStorage
        {
            public bool Exists(string slotId) => false;

            public SaveStorageReadResult Read(string slotId) =>
                SaveStorageReadResult.NotFound();

            public bool Write(string slotId, byte[] data) =>
                throw new InvalidOperationException("write failed");

            public bool Delete(string slotId) => false;
        }

        private sealed class UnreadableSaveStorage : ISaveStorage
        {
            public bool Exists(string slotId) => true;

            public SaveStorageReadResult Read(string slotId) =>
                SaveStorageReadResult.Failed("read failed");

            public bool Write(string slotId, byte[] data) => false;

            public bool Delete(string slotId) => false;
        }
    }

    [TestFixture]
    public sealed class InMemorySaveStorageTests
    {
        [Test]
        public void Read_ReturnsCopyOfStoredData()
        {
            var storage = new InMemorySaveStorage();
            storage.Write("slot", new byte[] { 1, 2, 3 });

            SaveStorageReadResult firstRead = storage.Read("slot");
            firstRead.Data[0] = 99;
            SaveStorageReadResult secondRead = storage.Read("slot");

            Assert.IsTrue(secondRead.IsSuccess);
            Assert.AreEqual(1, secondRead.Data[0]);
        }
    }

    [TestFixture]
    public sealed class LocalFileSaveStorageTests
    {
        [Test]
        public async Task WriteAsync_ReplacesExistingFileAndReadsLatestData()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "PJDev-SaveSystem-Tests",
                Guid.NewGuid().ToString("N"));

            try
            {
                var storage = new LocalFileSaveStorage(root);
                Assert.IsTrue(await storage.WriteAsync("slot", new byte[] { 1, 2 }));
                Assert.IsTrue(await storage.WriteAsync("slot", new byte[] { 3, 4, 5 }));

                SaveStorageReadResult result = await storage.ReadAsync("slot");

                Assert.IsTrue(result.IsSuccess);
                CollectionAssert.AreEqual(new byte[] { 3, 4, 5 }, result.Data);
                Assert.IsEmpty(Directory.GetFiles(root, "*.tmp"));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }
    }
}