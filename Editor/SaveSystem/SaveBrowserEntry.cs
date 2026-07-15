using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PJDev.DevelopKit.Framework.SaveSystem.Runtime;

namespace PJDev.DevelopKit.Framework.Editors.SaveSystem
{
    internal sealed class SaveBrowserEntry
    {
        public string SlotId;
        public string FilePath;
        public long FileSize;
        public DateTime ModifiedAt;
        public bool IsValid;
        public SaveFileMetadata Metadata;
        public string Error;
    }

    internal readonly struct SaveBrowserScanResult
    {
        public SaveBrowserScanResult(List<SaveBrowserEntry> entries, string error)
        {
            Entries = entries;
            Error = error;
        }

        public List<SaveBrowserEntry> Entries { get; }
        public string Error { get; }
    }

    internal static class SaveBrowserScanner
    {
        public static Task<SaveBrowserScanResult> ScanAsync(
            string directory,
            string extension,
            CancellationToken cancellationToken) =>
            Task.Run(() => Scan(directory, extension, cancellationToken), cancellationToken);

        private static SaveBrowserScanResult Scan(
            string directory,
            string extension,
            CancellationToken cancellationToken)
        {
            var entries = new List<SaveBrowserEntry>();
            if (string.IsNullOrWhiteSpace(directory))
                return new SaveBrowserScanResult(entries, "Save directory is empty.");

            if (!Directory.Exists(directory))
                return new SaveBrowserScanResult(entries, null);

            try
            {
                string pattern = "*" + NormalizeExtension(extension);
                foreach (string filePath in Directory.EnumerateFiles(
                             directory,
                             pattern,
                             SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entries.Add(ReadEntry(filePath, cancellationToken));
                }

                entries.Sort((left, right) => right.ModifiedAt.CompareTo(left.ModifiedAt));
                return new SaveBrowserScanResult(entries, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new SaveBrowserScanResult(entries, exception.Message);
            }
        }

        private static SaveBrowserEntry ReadEntry(
            string filePath,
            CancellationToken cancellationToken)
        {
            var entry = new SaveBrowserEntry
            {
                FilePath = filePath,
                SlotId = Path.GetFileNameWithoutExtension(filePath)
            };

            try
            {
                var fileInfo = new FileInfo(filePath);
                entry.FileSize = fileInfo.Length;
                entry.ModifiedAt = fileInfo.LastWriteTime;

                cancellationToken.ThrowIfCancellationRequested();
                byte[] fileBytes = File.ReadAllBytes(filePath);
                entry.IsValid = SaveFileInspector.TryInspect(fileBytes, out SaveFileMetadata metadata);
                entry.Metadata = metadata;
                if (!entry.IsValid)
                    entry.Error = "Header, length, version, or checksum is invalid.";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                entry.IsValid = false;
                entry.Error = exception.Message;
            }

            return entry;
        }

        private static string NormalizeExtension(string extension)
        {
            string value = string.IsNullOrWhiteSpace(extension) ? ".sav" : extension.Trim();
            return value[0] == '.' ? value : "." + value;
        }
    }
}