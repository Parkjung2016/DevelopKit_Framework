using System;
using System.Threading;
using System.Threading.Tasks;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed partial class SaveManager
    {
        public async Task<SaveResult> SaveAsync<T>(
            string slotId,
            T data,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveResult prepareResult = PrepareSave(
                slotId,
                data,
                out string normalizedSlotId,
                out byte[] fileBytes);

            if (!prepareResult.IsSuccess)
                return prepareResult;

            return await WriteAsync(normalizedSlotId, fileBytes, cancellationToken);
        }

        public async Task<SaveResult> SaveBytesAsync(
            string slotId,
            byte[] bytes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveResult prepareResult = PrepareBytes(
                slotId,
                bytes,
                out string normalizedSlotId,
                out byte[] fileBytes);

            if (!prepareResult.IsSuccess)
                return prepareResult;

            return await WriteAsync(normalizedSlotId, fileBytes, cancellationToken);
        }

        public async Task<LoadResult<T>> LoadAsync<T>(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            LoadResult<byte[]> bytesResult = await LoadBytesAsync(slotId, cancellationToken);
            return bytesResult.IsSuccess
                ? Deserialize<T>(bytesResult)
                : CopyFailure<T>(bytesResult);
        }

        public async Task<LoadResult<byte[]>> LoadBytesAsync(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
                return InvalidSlotLoadResult<byte[]>(slotId);

            cancellationToken.ThrowIfCancellationRequested();

            SaveStorageReadResult readResult;
            try
            {
                readResult = storage is IAsyncSaveStorage asyncStorage
                    ? await asyncStorage.ReadAsync(normalizedSlotId, cancellationToken)
                    : storage.Read(normalizedSlotId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return LoadResult<byte[]>.Failure(
                    SaveError.ReadFailed,
                    normalizedSlotId,
                    exception.Message);
            }

            return DecodeReadResult(normalizedSlotId, readResult);
        }

        public async Task<SaveResult> DeleteAsync(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetSlotId(slotId, out string normalizedSlotId, out SaveResult failure))
                return failure;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                bool deleted = storage is IAsyncSaveStorage asyncStorage
                    ? await asyncStorage.DeleteAsync(normalizedSlotId, cancellationToken)
                    : storage.Delete(normalizedSlotId);

                return deleted
                    ? SaveResult.Success(normalizedSlotId)
                    : SaveResult.Failure(SaveError.SlotNotFound, normalizedSlotId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveError.DeleteFailed,
                    normalizedSlotId,
                    exception.Message);
            }
        }

        private async Task<SaveResult> WriteAsync(
            string normalizedSlotId,
            byte[] fileBytes,
            CancellationToken cancellationToken)
        {
            try
            {
                bool written = storage is IAsyncSaveStorage asyncStorage
                    ? await asyncStorage.WriteAsync(normalizedSlotId, fileBytes, cancellationToken)
                    : storage.Write(normalizedSlotId, fileBytes);

                return written
                    ? SaveResult.Success(normalizedSlotId)
                    : SaveResult.Failure(SaveError.WriteFailed, normalizedSlotId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveError.WriteFailed,
                    normalizedSlotId,
                    exception.Message);
            }
        }
    }
}