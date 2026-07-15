using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed class InMemorySaveStorage : ISaveStorage, IAsyncSaveStorage
    {
        private readonly Dictionary<string, byte[]> entries = new(StringComparer.Ordinal);
        private readonly object syncRoot = new();

        public bool Exists(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
                return false;

            lock (syncRoot)
                return entries.ContainsKey(normalizedSlotId);
        }

        public SaveStorageReadResult Read(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
                return SaveStorageReadResult.NotFound();

            lock (syncRoot)
            {
                return entries.TryGetValue(normalizedSlotId, out byte[] stored)
                    ? SaveStorageReadResult.Success(Copy(stored))
                    : SaveStorageReadResult.NotFound();
            }
        }

        public bool Write(string slotId, byte[] data)
        {
            if (data == null
                || data.Length == 0
                || !SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
            {
                return false;
            }

            lock (syncRoot)
            {
                entries[normalizedSlotId] = Copy(data);
                return true;
            }
        }

        public bool Delete(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalizedSlotId))
                return false;

            lock (syncRoot)
                return entries.Remove(normalizedSlotId);
        }

        public Task<SaveStorageReadResult> ReadAsync(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(slotId));
        }

        public Task<bool> WriteAsync(
            string slotId,
            byte[] data,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Write(slotId, data));
        }

        public Task<bool> DeleteAsync(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Delete(slotId));
        }

        public void Clear()
        {
            lock (syncRoot)
                entries.Clear();
        }

        private static byte[] Copy(byte[] source)
        {
            var copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }
    }
}