using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public static class SaveSlotId
    {
        private static readonly Regex InvalidFileNameChars = new(@"[\\/:*?""<>|]", RegexOptions.Compiled);

        public static bool TryNormalize(string slotId, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(slotId))
                return false;

            normalized = InvalidFileNameChars.Replace(slotId.Trim(), "_");
            return normalized.Length > 0;
        }
    }

    public sealed class LocalFileSaveStorage : ISaveStorage
    {
        private readonly string rootDirectory;
        private readonly string fileExtension;

        public LocalFileSaveStorage(string rootDirectory = null, string fileExtension = ".sav")
        {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "Saves")
                : rootDirectory;
            this.fileExtension = string.IsNullOrWhiteSpace(fileExtension) ? ".sav" : fileExtension;
        }

        public string RootDirectory => rootDirectory;

        public bool Exists(string slotId) => File.Exists(GetPath(slotId));

        public bool TryRead(string slotId, out byte[] data)
        {
            data = null;
            if (!TryGetPath(slotId, out string path))
                return false;

            if (!File.Exists(path))
                return false;

            try
            {
                data = File.ReadAllBytes(path);
                return data.Length > 0;
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

        public bool TryWrite(string slotId, byte[] data)
        {
            if (data == null || data.Length == 0 || !TryGetPath(slotId, out string path))
                return false;

            try
            {
                Directory.CreateDirectory(rootDirectory);
                File.WriteAllBytes(path, data);
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

        public bool TryDelete(string slotId)
        {
            if (!TryGetPath(slotId, out string path) || !File.Exists(path))
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
            if (!SaveSlotId.TryNormalize(slotId, out string normalized))
                return false;

            path = Path.Combine(rootDirectory, normalized + fileExtension);
            return true;
        }

        private string GetPath(string slotId)
        {
            TryGetPath(slotId, out string path);
            return path;
        }
    }
}
