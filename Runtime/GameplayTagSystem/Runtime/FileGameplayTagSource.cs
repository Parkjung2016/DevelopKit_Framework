using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>
    /// ProjectSettings/GameplayTags/*.json 파일에서 태그를 읽고 쓰는 소스입니다.
    /// </summary>
    internal sealed class FileGameplayTagSource : IGameplayTagSource, IDeleteTagHandler, IGameplayTagEditHandler
    {
        private static readonly Dictionary<string, FileGameplayTagSource> SourcesByPath =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<FileGameplayTagSource> SourceBuffer = new();
        private static readonly HashSet<string> SeenPaths = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> RemovedPathBuffer = new();
        private struct TagInFile
        {
            public string Name;
            public string Comment;
        }

        /// <summary>JSON 태그 파일이 저장되는 프로젝트 상대 디렉터리입니다.</summary>
        public static readonly string DirectoryPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "ProjectSettings", "GameplayTags"));

        private readonly List<TagInFile> tags = new();
        private JObject root;
        private long loadedWriteTicks;
        private long loadedLength;

        public string Name { get; private set; }

        public string FilePath { get; private set; }

        public bool IsReadOnly => false;

        public FileGameplayTagSource(string filePath)
        {
            FilePath = filePath;
            Name = Path.GetFileName(filePath);
        }

        /// <summary>디스크에서 JSON을 읽어 메모리에 로드합니다.</summary>
        public bool TryLoad()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    root = new JObject();
                    tags.Clear();
                    loadedWriteTicks = 0;
                    loadedLength = 0;
                    return true;
                }

                root = LoadRoot();
                RebuildTagCache();
                UpdateFileStamp();
                return true;
            }
            catch (Exception ex)
            {
                CDebug.LogError($"태그 파일 '{Name}' 로드 실패: {ex.Message}");
                return false;
            }
        }

        private bool ReloadIfChanged()
        {
            if (!File.Exists(FilePath))
                return false;

            FileInfo info = new(FilePath);
            if (root != null && info.LastWriteTimeUtc.Ticks == loadedWriteTicks && info.Length == loadedLength)
                return true;

            return TryLoad();
        }

        /// <summary>디렉터리에 있는 JSON 태그 소스를 캐시해 반환합니다.</summary>
        public static IReadOnlyList<FileGameplayTagSource> GetAllFileSources()
        {
            SourceBuffer.Clear();
            SeenPaths.Clear();

            if (!Directory.Exists(DirectoryPath))
            {
                SourcesByPath.Clear();
                return SourceBuffer;
            }

            foreach (string rawPath in Directory.EnumerateFiles(DirectoryPath, "*.json"))
            {
                string path = Path.GetFullPath(rawPath);
                SeenPaths.Add(path);

                if (!SourcesByPath.TryGetValue(path, out FileGameplayTagSource source))
                {
                    source = new FileGameplayTagSource(path);
                    SourcesByPath.Add(path, source);
                }

                if (source.ReloadIfChanged())
                    SourceBuffer.Add(source);
            }

            RemovedPathBuffer.Clear();
            foreach (string cachedPath in SourcesByPath.Keys)
            {
                if (!SeenPaths.Contains(cachedPath))
                    RemovedPathBuffer.Add(cachedPath);
            }

            for (int i = 0; i < RemovedPathBuffer.Count; i++)
                SourcesByPath.Remove(RemovedPathBuffer[i]);

            SourceBuffer.Sort(static (a, b) =>
                string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));
            return SourceBuffer;
        }

        public void RegisterTags(GameplayTagRegistrationContext context)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                TagInFile tag = tags[i];
                try
                {
                    context.RegisterTag(tag.Name, tag.Comment, GameplayTagFlags.None, this);
                }
                catch (Exception ex)
                {
                    CDebug.LogError($"태그 '{tag.Name}' 등록 실패 (파일: '{FilePath}').");
                    CDebug.LogError(ex);
                }
            }
        }
        /// <summary>이 파일에 지정한 이름의 태그가 있는지 확인합니다.</summary>
        public bool ContainsTag(string tagName) =>
            root != null && root.ContainsKey(tagName);

        /// <summary>다른 JSON 파일에 있는 태그 소스를 찾습니다.</summary>
        public static FileGameplayTagSource FindSourceContainingTag(string tagName, string excludeSourceFileName = null)
        {
            foreach (FileGameplayTagSource source in GetAllFileSources())
            {
                if (!string.IsNullOrEmpty(excludeSourceFileName) &&
                    string.Equals(source.Name, excludeSourceFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (source.ContainsTag(tagName))
                    return source;
            }

            return null;
        }

        public void AddTag(string tagName, string comment)
        {
            if (!TryAddTag(tagName, comment, out string errorMessage))
                throw new InvalidOperationException(errorMessage);
        }

        public bool TryAddTag(string tagName, string comment, out string errorMessage)
        {
            errorMessage = null;

            if (root == null)
            {
                errorMessage = "TAG_SOURCE_NOT_LOADED";
                return false;
            }

            if (!GameplayTagUtility.IsNameValid(tagName, out errorMessage))
                return false;

            if (root.ContainsKey(tagName))
            {
                errorMessage = $"TAG_ALREADY_EXISTS:{tagName}:{Name}";
                return false;
            }

            FileGameplayTagSource existingSource = FindSourceContainingTag(tagName, Name);
            if (existingSource != null)
            {
                errorMessage = $"TAG_ALREADY_EXISTS:{tagName}:{existingSource.Name}";
                return false;
            }

            try
            {
                EnsureMissingParents(tagName);

                JObject newTagObject = new();
                if (!string.IsNullOrEmpty(comment))
                    newTagObject["Comment"] = comment;

                root.Add(tagName, newTagObject);
                SaveFile();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>다른 JSON 파일에서 태그 계층을 잘라 이 파일로 옮깁니다.</summary>
        public bool TryMoveTagHierarchyTo(FileGameplayTagSource target, string tagName, out string errorMessage)
        {
            errorMessage = null;

            if (target == null)
            {
                errorMessage = "TAG_SOURCE_NOT_LOADED";
                return false;
            }

            if (ReferenceEquals(this, target))
                return true;

            if (!TryCutTagHierarchy(tagName, out Dictionary<string, JToken> tags, out errorMessage))
                return false;

            if (!target.TryImportTagHierarchy(tags, tagName, out errorMessage))
            {
                RestoreCutTags(tags);
                return false;
            }

            return true;
        }

        public bool TryValidateDelete(string tagName, GameplayTagDeleteMode mode, out string errorMessage)
        {
            errorMessage = null;

            if (root == null)
                return true;

            if (mode == GameplayTagDeleteMode.Hierarchy)
                return true;

            return TryValidateDeleteTagOnly(tagName, out errorMessage);
        }

        public bool TryDeleteTag(string tagName, GameplayTagDeleteMode mode, out string errorMessage)
        {
            errorMessage = null;

            if (root == null)
                return true;

            if (mode == GameplayTagDeleteMode.Hierarchy)
            {
                if (!ContainsTagHierarchy(tagName))
                {
                    return true;
                }

                try
                {
                    DeleteTagHierarchy(tagName);
                    return true;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }
            }

            if (!TryValidateDeleteTagOnly(tagName, out errorMessage))
                return false;

            try
            {
                ApplyDeleteTagOnly(tagName);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public void DeleteTag(string tagName, GameplayTagDeleteMode mode = GameplayTagDeleteMode.TagOnly)
        {
            if (!TryDeleteTag(tagName, mode, out string errorMessage))
                throw new InvalidOperationException(errorMessage);
        }

        public bool TryUpdateComment(string tagName, string comment, out string errorMessage)
        {
            errorMessage = null;

            if (root == null)
            {
                errorMessage = "TAG_SOURCE_NOT_LOADED";
                return false;
            }

            if (!root.TryGetValue(tagName, out JToken tagToken) || tagToken is not JObject tagObject)
            {
                errorMessage = $"TAG_NOT_IN_FILE:{tagName}";
                return false;
            }

            try
            {
                if (string.IsNullOrEmpty(comment))
                    tagObject.Remove("Comment");
                else
                    tagObject["Comment"] = comment;

                SaveFile();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool TryRenameTag(string oldName, string newName, out string errorMessage, bool createMissingParents = false)
        {
            errorMessage = null;

            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return true;

            if (root == null)
            {
                errorMessage = "TAG_SOURCE_NOT_LOADED";
                return false;
            }

            if (!root.ContainsKey(oldName))
            {
                errorMessage = $"TAG_NOT_IN_FILE:{oldName}";
                return false;
            }

            if (!GameplayTagUtility.IsNameValid(newName, out errorMessage))
                return false;

            if (root.ContainsKey(newName))
            {
                errorMessage = $"TAG_ALREADY_EXISTS:{newName}:{Name}";
                return false;
            }

            string oldPrefix = oldName + ".";
            string newPrefix = newName + ".";
            List<(string from, string to, JToken value)> renames = new();

            foreach (JProperty property in root.Properties())
            {
                string name = property.Name;
                if (name == oldName)
                {
                    renames.Add((oldName, newName, property.Value.DeepClone()));
                    continue;
                }

                if (name.StartsWith(oldPrefix, StringComparison.Ordinal))
                    renames.Add((name, newPrefix + name.Substring(oldPrefix.Length), property.Value.DeepClone()));
            }

            HashSet<string> namesBeingRenamed = new(StringComparer.Ordinal);
            foreach ((string from, string to, _) in renames)
            {
                namesBeingRenamed.Add(from);
                if (string.Equals(to, from, StringComparison.Ordinal))
                    continue;

                if (root.ContainsKey(to) && !namesBeingRenamed.Contains(to))
                {
                    errorMessage = $"TAG_RENAME_CONFLICT:{newName}:{to}";
                    return false;
                }

                if (!namesBeingRenamed.Add(to))
                {
                    errorMessage = $"TAG_RENAME_DUPLICATE:{newName}";
                    return false;
                }
            }

            try
            {
                if (createMissingParents)
                    EnsureMissingParents(newName);

                foreach ((string from, _, _) in renames)
                    root.Remove(from);

                foreach ((string _, string to, JToken value) in renames)
                    root[to] = value;

                SaveFile();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private void RebuildTagCache()
        {
            tags.Clear();
            if (root == null)
                return;

            foreach (JProperty property in root.Properties())
            {
                string comment = property.Value["Comment"]?.ToString();
                tags.Add(new TagInFile { Name = property.Name, Comment = comment });
            }
        }

        private bool ContainsTagHierarchy(string tagName)
        {
            string prefix = tagName + ".";
            foreach (JProperty property in root.Properties())
            {
                if (property.Name == tagName || property.Name.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void UpdateFileStamp()
        {
            if (!File.Exists(FilePath))
            {
                loadedWriteTicks = 0;
                loadedLength = 0;
                return;
            }

            FileInfo info = new(FilePath);
            loadedWriteTicks = info.LastWriteTimeUtc.Ticks;
            loadedLength = info.Length;
        }

        private bool TryImportTagHierarchy(
            Dictionary<string, JToken> tags,
            string rootTagName,
            out string errorMessage)
        {
            errorMessage = null;

            if (root == null)
            {
                errorMessage = "TAG_SOURCE_NOT_LOADED";
                return false;
            }

            try
            {
                EnsureMissingParents(rootTagName);

                foreach (KeyValuePair<string, JToken> entry in tags)
                {
                    if (root.ContainsKey(entry.Key))
                    {
                        errorMessage = $"TAG_ALREADY_EXISTS:{entry.Key}:{Name}";
                        return false;
                    }

                    root[entry.Key] = entry.Value;
                }

                SaveFile();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private void EnsureMissingParents(string tagName)
        {
            string[] hierarchy = GameplayTagUtility.GetHierarchyNames(tagName);

            for (int i = 0; i < hierarchy.Length - 1; i++)
            {
                string parentName = hierarchy[i];
                if (root.ContainsKey(parentName))
                    continue;

                root.Add(parentName, new JObject());
            }
        }

        private JObject LoadRoot()
        {
            string fileContent = File.ReadAllText(FilePath);
            return JObject.Parse(fileContent);
        }

        private void SaveFile()
        {
            string fileContent = root.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, fileContent);
            RebuildTagCache();
            UpdateFileStamp();
        }

        private bool TryValidateDeleteTagOnly(string tagName, out string errorMessage)
        {
            errorMessage = null;

            if (!root.ContainsKey(tagName))
                return true;

            string descendantPrefix = tagName + ".";
            List<(string oldName, string newName)> promotions = new();
            HashSet<string> namesBeingRemoved = new(StringComparer.Ordinal) { tagName };

            foreach (JProperty property in root.Properties())
            {
                string name = property.Name;
                if (!name.StartsWith(descendantPrefix, StringComparison.Ordinal))
                    continue;

                promotions.Add((name, name.Substring(descendantPrefix.Length)));
                namesBeingRemoved.Add(name);
            }

            HashSet<string> promotionTargets = new(StringComparer.Ordinal);
            foreach ((string _, string newName) in promotions)
            {
                if (!promotionTargets.Add(newName))
                {
                    errorMessage = $"TAG_DELETE_PROMOTE_DUPLICATE:{tagName}";
                    return false;
                }

                if (root.ContainsKey(newName) && !namesBeingRemoved.Contains(newName))
                {
                    errorMessage = $"TAG_DELETE_PROMOTE_CONFLICT:{tagName}:{newName}";
                    return false;
                }
            }

            return true;
        }

        private void ApplyDeleteTagOnly(string tagName)
        {
            if (!root.ContainsKey(tagName))
                return;

            string descendantPrefix = tagName + ".";
            List<(string oldName, string newName, JToken value)> promotions = new();

            foreach (JProperty property in root.Properties())
            {
                string name = property.Name;
                if (!name.StartsWith(descendantPrefix, StringComparison.Ordinal))
                    continue;

                promotions.Add((name, name.Substring(descendantPrefix.Length), property.Value.DeepClone()));
            }

            root.Remove(tagName);

            foreach ((string oldName, _, _) in promotions)
                root.Remove(oldName);

            foreach ((string _, string newName, JToken value) in promotions)
                root[newName] = value;

            SaveFile();
        }

        private void DeleteTagHierarchy(string tagName)
        {
            string descendantPrefix = tagName + ".";
            List<string> namesToRemove = new();

            foreach (JProperty property in root.Properties())
            {
                string name = property.Name;
                if (name == tagName || name.StartsWith(descendantPrefix, StringComparison.Ordinal))
                    namesToRemove.Add(name);
            }

            foreach (string name in namesToRemove)
                root.Remove(name);

            SaveFile();
        }

        private bool TryCutTagHierarchy(string tagName, out Dictionary<string, JToken> tags, out string errorMessage)
        {
            tags = new Dictionary<string, JToken>(StringComparer.Ordinal);
            errorMessage = null;

            if (root == null || !root.ContainsKey(tagName))
            {
                errorMessage = $"TAG_NOT_IN_FILE:{tagName}";
                return false;
            }

            string prefix = tagName + ".";
            foreach (JProperty property in root.Properties())
            {
                string name = property.Name;
                if (name == tagName || name.StartsWith(prefix, StringComparison.Ordinal))
                    tags[name] = property.Value.DeepClone();
            }

            foreach (string name in tags.Keys)
                root.Remove(name);

            SaveFile();
            return true;
        }

        private void RestoreCutTags(Dictionary<string, JToken> tags)
        {
            if (root == null)
                return;

            foreach (KeyValuePair<string, JToken> entry in tags)
                root[entry.Key] = entry.Value;

            SaveFile();
        }
    }
}
