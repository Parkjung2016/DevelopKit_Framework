using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryEnumAssemblyConfigurator
    {
        private static readonly string[] AssemblyAssetPaths =
        {
            InventoryEnumPaths.RuntimeAssemblyAssetPath,
            InventoryEnumPaths.EditorsAssemblyAssetPath,
            InventoryEnumPaths.TestsAssemblyAssetPath
        };

        public static bool IsGeneratedModeEnabled() =>
            HasDefineSymbol(InventoryEnumPaths.DefineSymbol);

        public static bool HasGeneratedEnumFiles() =>
            File.Exists(Path.GetFullPath(InventoryEnumPaths.ContainerKindAssetPath));

        public static bool EnableGeneratedMode()
        {
            bool changed = false;
            changed |= SetDefineSymbol(InventoryEnumPaths.DefineSymbol, true);
            changed |= WriteGeneratedAssemblyDefinition();
            changed |= SetGeneratedAssemblyReference(true);
            return changed;
        }

        public static bool DisableGeneratedMode()
        {
            bool changed = false;
            changed |= SetDefineSymbol(InventoryEnumPaths.DefineSymbol, false);
            changed |= SetGeneratedAssemblyReference(false);
            return changed;
        }

        public static bool SyncGeneratedMode()
        {
            if (HasGeneratedEnumFiles())
                return EnableGeneratedMode();

            if (IsGeneratedModeEnabled())
                return DisableGeneratedMode();

            return false;
        }

        private static bool WriteGeneratedAssemblyDefinition()
        {
            string assetPath = InventoryEnumPaths.GeneratedAssemblyAssetPath;
            string fullPath = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string content =
                "{\n" +
                $"    \"name\": \"{InventoryEnumPaths.GeneratedAssemblyName}\",\n" +
                "    \"rootNamespace\": \"PJDev.DevelopKit.Framework.InventorySystem.Runtime\",\n" +
                "    \"references\": [],\n" +
                "    \"includePlatforms\": [],\n" +
                "    \"excludePlatforms\": [],\n" +
                "    \"allowUnsafeCode\": false,\n" +
                "    \"overrideReferences\": false,\n" +
                "    \"precompiledReferences\": [],\n" +
                "    \"autoReferenced\": true,\n" +
                $"    \"defineConstraints\": [\n" +
                $"        \"{InventoryEnumPaths.DefineSymbol}\"\n" +
                "    ],\n" +
                "    \"versionDefines\": [],\n" +
                "    \"noEngineReferences\": false\n" +
                "}\n";

            string metaContent =
                "fileFormatVersion: 2\n" +
                $"guid: {InventoryEnumPaths.GeneratedAssemblyGuid}\n" +
                "AssemblyDefinitionImporter:\n" +
                "  externalObjects: {}\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n";

            bool changed = false;
            changed |= WriteIfChanged(fullPath, content);
            changed |= WriteIfChanged(fullPath + ".meta", metaContent);
            return changed;
        }

        private static bool SetGeneratedAssemblyReference(bool enabled)
        {
            bool changed = false;
            for (int i = 0; i < AssemblyAssetPaths.Length; i++)
                changed |= SetAssemblyReference(AssemblyAssetPaths[i], InventoryEnumPaths.GeneratedAssemblyGuid, enabled);

            return changed;
        }

        private static bool SetAssemblyReference(string asmdefAssetPath, string guid, bool enabled)
        {
            string fullPath = Path.GetFullPath(asmdefAssetPath);
            if (!File.Exists(fullPath))
                return false;

            string json = File.ReadAllText(fullPath);
            string referenceToken = $"GUID:{guid}";
            bool hasReference = json.Contains(referenceToken, StringComparison.Ordinal);
            if (enabled == hasReference)
                return false;

            if (enabled)
            {
                const string marker = "\"references\": [";
                int index = json.IndexOf(marker, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int insertIndex = index + marker.Length;
                string insertion = $"\n        \"{referenceToken}\",";
                json = json.Insert(insertIndex, insertion);
            }
            else
            {
                json = json.Replace($"        \"{referenceToken}\",\n", string.Empty, StringComparison.Ordinal);
                json = json.Replace($"        \"{referenceToken}\"\n", string.Empty, StringComparison.Ordinal);
                json = json.Replace($",\n        \"{referenceToken}\"", string.Empty, StringComparison.Ordinal);
                json = json.Replace($"\n        \"{referenceToken}\"", string.Empty, StringComparison.Ordinal);
            }

            File.WriteAllText(fullPath, json, Encoding.UTF8);
            return true;
        }

        private static bool SetDefineSymbol(string symbol, bool enabled)
        {
            bool changed = false;
            foreach (BuildTargetGroup group in EnumerateDefineGroups())
            {
                string defines = GetScriptingDefineSymbols(group);
                var defineList = defines.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                bool contains = defineList.Contains(symbol, StringComparer.Ordinal);

                if (enabled && contains)
                    continue;

                if (!enabled && !contains)
                    continue;

                if (enabled)
                    defineList.Add(symbol);
                else
                    defineList.Remove(symbol);

                SetScriptingDefineSymbols(
                    group,
                    string.Join(";", defineList.Where(static value => !string.IsNullOrWhiteSpace(value))));
                changed = true;
            }

            return changed;
        }

        private static bool HasDefineSymbol(string symbol)
        {
            foreach (BuildTargetGroup group in EnumerateDefineGroups())
            {
                string defines = GetScriptingDefineSymbols(group);
                if (defines.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(symbol, StringComparer.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<BuildTargetGroup> EnumerateDefineGroups()
        {
            var groups = new List<BuildTargetGroup>();
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown)
                    continue;

                FieldInfo field = typeof(BuildTargetGroup).GetField(group.ToString());
                if (field != null && field.IsDefined(typeof(ObsoleteAttribute), false))
                    continue;

                try
                {
                    GetScriptingDefineSymbols(group);
                    groups.Add(group);
                }
                catch (ArgumentException)
                {
                }
            }

            return groups;
        }

        private static string GetScriptingDefineSymbols(BuildTargetGroup group)
        {
#if UNITY_2023_1_OR_NEWER
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif
        }

        private static void SetScriptingDefineSymbols(BuildTargetGroup group, string defines)
        {
#if UNITY_2023_1_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group), defines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
#endif
        }

        private static bool WriteIfChanged(string fullPath, string content)
        {
            if (File.Exists(fullPath))
            {
                string existing = File.ReadAllText(fullPath);
                if (string.Equals(existing, content, StringComparison.Ordinal))
                    return false;
            }

            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, content, Encoding.UTF8);
            return true;
        }
    }
}
