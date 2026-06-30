using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, byte[]> entries = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> SlotIds => entries.Keys;

        public bool Exists(string slotId) =>
            SaveSlotId.TryNormalize(slotId, out string normalized) && entries.ContainsKey(normalized);

        public bool TryRead(string slotId, out byte[] data)
        {
            data = null;
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return false;

            return entries.TryGetValue(normalized, out data) && data != null && data.Length > 0;
        }

        public bool TryWrite(string slotId, byte[] data)
        {
            if (data == null || data.Length == 0 || !SaveSlotId.TryNormalize(slotId, out string normalized))
                return false;

            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            entries[normalized] = copy;
            return true;
        }

        public bool TryDelete(string slotId)
        {
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return false;

            return entries.Remove(normalized);
        }

        public void Clear() => entries.Clear();
    }
}
