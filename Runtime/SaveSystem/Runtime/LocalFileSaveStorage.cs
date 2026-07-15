using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed class LocalFileSaveStorage : ISaveStorage, IAsyncSaveStorage
    {
        private const int BufferSize = 4096;

        private readonly string saveDirectory;
        private readonly string fileExtension;
        private readonly SemaphoreSlim fileLock = new(1, 1);

        public LocalFileSaveStorage(string saveDirectory = null, string fileExtension = ".sav")
        {
            this.saveDirectory = Path.GetFullPath(
                string.IsNullOrWhiteSpace(saveDirectory)
                    ? Path.Combine(Application.persistentDataPath, "Saves")
                    : saveDirectory);
            this.fileExtension = NormalizeExtension(fileExtension);
        }

        public string SaveDirectory => saveDirectory;
        public string FileExtension => fileExtension;

        public bool Exists(string slotId) =>
            TryGetPath(slotId, out string path) && File.Exists(path);

        public SaveStorageReadResult Read(string slotId)
        {
            if (!TryGetPath(slotId, out string path) || !File.Exists(path))
                return SaveStorageReadResult.NotFound();

            try
            {
                byte[] data = File.ReadAllBytes(path);
                return data.Length > 0
                    ? SaveStorageReadResult.Success(data)
                    : SaveStorageReadResult.Failed("The save file is empty.");
            }
            catch (FileNotFoundException)
            {
                return SaveStorageReadResult.NotFound();
            }
            catch (IOException exception)
            {
                return SaveStorageReadResult.Failed(exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SaveStorageReadResult.Failed(exception.Message);
            }
        }

        public bool Write(string slotId, byte[] data)
        {
            if (data == null || data.Length == 0 || !TryGetPath(slotId, out string path))
                return false;

            fileLock.Wait();
            try
            {
                return TryWriteFile(path, data);
            }
            finally
            {
                fileLock.Release();
            }
        }

        public bool Delete(string slotId)
        {
            if (!TryGetPath(slotId, out string path))
                return false;

            fileLock.Wait();
            try
            {
                return TryDeleteFile(path);
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async Task<SaveStorageReadResult> ReadAsync(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetPath(slotId, out string path) || !File.Exists(path))
                return SaveStorageReadResult.NotFound();

            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                if (stream.Length <= 0)
                    return SaveStorageReadResult.Failed("The save file is empty.");

                if (stream.Length > int.MaxValue)
                    return SaveStorageReadResult.Failed("The save file is too large.");

                var data = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int read = await stream.ReadAsync(
                        data,
                        offset,
                        data.Length - offset,
                        cancellationToken).ConfigureAwait(false);

                    if (read == 0)
                        return SaveStorageReadResult.Failed("The save file ended unexpectedly.");

                    offset += read;
                }

                return SaveStorageReadResult.Success(data);
            }
            catch (FileNotFoundException)
            {
                return SaveStorageReadResult.NotFound();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException exception)
            {
                return SaveStorageReadResult.Failed(exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SaveStorageReadResult.Failed(exception.Message);
            }
        }

        public async Task<bool> WriteAsync(
            string slotId,
            byte[] data,
            CancellationToken cancellationToken = default)
        {
            if (data == null || data.Length == 0 || !TryGetPath(slotId, out string path))
                return false;

            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await TryWriteFileAsync(path, data, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async Task<bool> DeleteAsync(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetPath(slotId, out string path))
                return false;

            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return TryDeleteFile(path);
            }
            finally
            {
                fileLock.Release();
            }
        }

        private bool TryWriteFile(string path, byte[] data)
        {
            string temporaryPath = CreateTemporaryPath(path);
            try
            {
                Directory.CreateDirectory(saveDirectory);
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           BufferSize,
                           FileOptions.WriteThrough))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                CommitFile(temporaryPath, path);
                temporaryPath = null;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                    TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private async Task<bool> TryWriteFileAsync(
            string path,
            byte[] data,
            CancellationToken cancellationToken)
        {
            string temporaryPath = CreateTemporaryPath(path);
            try
            {
                Directory.CreateDirectory(saveDirectory);
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           BufferSize,
                           FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                CommitFile(temporaryPath, path);
                temporaryPath = null;
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                    TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private static void CommitFile(string temporaryPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
                File.Replace(temporaryPath, destinationPath, null);
            else
                File.Move(temporaryPath, destinationPath);
        }

        private static bool TryDeleteFile(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private bool TryGetPath(string slotId, out string path)
        {
            path = null;
            if (!SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
                return false;

            path = Path.Combine(saveDirectory, normalizedSlotId + fileExtension);
            return true;
        }

        private static string NormalizeExtension(string extension)
        {
            string value = string.IsNullOrWhiteSpace(extension) ? ".sav" : extension.Trim();
            if (value[0] != '.')
                value = "." + value;

            if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || value.IndexOf(Path.DirectorySeparatorChar) >= 0
                || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException("The save file extension contains invalid characters.", nameof(extension));
            }

            return value;
        }

        private static string CreateTemporaryPath(string path) =>
            path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        private static void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}