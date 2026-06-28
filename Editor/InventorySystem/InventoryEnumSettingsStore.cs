using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    [Serializable]
    internal sealed class InventoryEnumEntryData
    {
        public string name;
        public int value;
        public string displayName;
        public string description;
    }

    [Serializable]
    internal sealed class InventoryItemTypeRouteData
    {
        public int itemTypeValue;
        public int[] containerKindValues = Array.Empty<int>();
    }

    [Serializable]
    internal sealed class InventoryEnumsDocument
    {
        public InventoryEnumEntryData[] itemTypes = Array.Empty<InventoryEnumEntryData>();
        public InventoryEnumEntryData[] containerKinds = Array.Empty<InventoryEnumEntryData>();
        public InventoryItemTypeRouteData[] itemTypeRoutes = Array.Empty<InventoryItemTypeRouteData>();
    }

    internal static class InventoryEnumSettingsStore
    {
        public const string RelativeDirectory = "ProjectSettings/InventorySystem";
        public const string RelativeFilePath = RelativeDirectory + "/InventoryEnums.json";

        public static string FullPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelativeFilePath));

        public static InventoryEnumsDocument LoadOrCreateDefault()
        {
            if (!File.Exists(FullPath))
            {
                InventoryEnumsDocument defaults = CreateDefaultDocument();
                Save(defaults);
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(FullPath);
                InventoryEnumsDocument document = JsonUtility.FromJson<InventoryEnumsDocument>(json);
                if (document == null)
                    return CreateDefaultDocument();

                document.itemTypes ??= Array.Empty<InventoryEnumEntryData>();
                document.containerKinds ??= Array.Empty<InventoryEnumEntryData>();
                document.itemTypeRoutes ??= Array.Empty<InventoryItemTypeRouteData>();
                return document;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Inventory enum settings load failed: {ex.Message}");
                return CreateDefaultDocument();
            }
        }

        public static void Save(InventoryEnumsDocument document)
        {
            string directory = Path.GetDirectoryName(FullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(document, true);
            File.WriteAllText(FullPath, json);
        }

        public static InventoryEnumsDocument CreateDefaultDocument() =>
            new()
            {
                itemTypes = CreateDefaultItemTypes(),
                containerKinds = CreateDefaultContainerKinds(),
                itemTypeRoutes = CreateDefaultRoutes()
            };

        private static InventoryEnumEntryData[] CreateDefaultItemTypes() =>
            new[]
            {
                Entry("None", 0, "None", "미지정 / 필터 제외용"),
                Entry("General", 1, "General", "일반 아이템"),
                Entry("Consumable", 2, "Consumable", "소비 아이템"),
                Entry("Equipment", 3, "Equipment", "장비"),
                Entry("Material", 4, "Material", "재료"),
                Entry("Quest", 5, "Quest", "퀘스트 아이템"),
                Entry("Currency", 6, "Currency", "화폐")
            };

        private static InventoryEnumEntryData[] CreateDefaultContainerKinds() =>
            new[]
            {
                Entry("Main", 0, "Main", "기본 인벤토리"),
                Entry("Equipment", 1, "Equipment", "장비 슬롯"),
                Entry("QuickBar", 2, "QuickBar", "퀵바"),
                Entry("Stash", 3, "Stash", "창고"),
                Entry("Quest", 4, "Quest", "퀘스트 전용")
            };

        private static InventoryItemTypeRouteData[] CreateDefaultRoutes() =>
            new[]
            {
                Route(3, 1, 0),
                Route(5, 4, 0),
                Route(2, 2, 0),
                Route(4, 0, 3),
                Route(6, 0),
                Route(1, 0)
            };

        private static InventoryEnumEntryData Entry(string name, int value, string displayName, string description) =>
            new()
            {
                name = name,
                value = value,
                displayName = displayName,
                description = description
            };

        private static InventoryItemTypeRouteData Route(int itemTypeValue, params int[] containerKindValues) =>
            new()
            {
                itemTypeValue = itemTypeValue,
                containerKindValues = containerKindValues
            };

        public static bool TryValidate(InventoryEnumsDocument document, out string errorMessage)
        {
            errorMessage = null;
            if (document == null)
            {
                errorMessage = "문서가 비어 있습니다.";
                return false;
            }

            if (!TryValidateEntries(document.itemTypes, "ItemType", requireZeroEntryNamedNone: true, out errorMessage))
                return false;

            if (!TryValidateEntries(document.containerKinds, "ContainerKind", requireZeroEntryNamedNone: false, out errorMessage))
                return false;

            if (!TryValidateRoutes(document, out errorMessage))
                return false;

            return true;
        }

        private static bool TryValidateEntries(
            InventoryEnumEntryData[] entries,
            string enumLabel,
            bool requireZeroEntryNamedNone,
            out string errorMessage)
        {
            errorMessage = null;
            if (entries == null || entries.Length == 0)
            {
                errorMessage = $"{enumLabel} 항목이 없습니다.";
                return false;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var values = new HashSet<int>();

            for (int i = 0; i < entries.Length; i++)
            {
                InventoryEnumEntryData entry = entries[i];
                if (entry == null)
                {
                    errorMessage = $"{enumLabel} 항목 {i + 1}이 비어 있습니다.";
                    return false;
                }

                if (!InventoryEnumScriptGenerator.TrySanitizeIdentifier(entry.name, out string sanitized, out string nameError))
                {
                    errorMessage = $"{enumLabel} '{entry.name}': {nameError}";
                    return false;
                }

                if (!names.Add(sanitized))
                {
                    errorMessage = $"{enumLabel} 이름이 중복됩니다: {sanitized}";
                    return false;
                }

                if (!values.Add(entry.value))
                {
                    errorMessage = $"{enumLabel} 값이 중복됩니다: {entry.value}";
                    return false;
                }

                entry.name = sanitized;
                entry.displayName = string.IsNullOrWhiteSpace(entry.displayName) ? sanitized : entry.displayName.Trim();
                entry.description ??= string.Empty;
            }

            if (requireZeroEntryNamedNone)
            {
                bool hasNoneAtZero = false;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].value == 0)
                    {
                        hasNoneAtZero = true;
                        break;
                    }
                }

                if (!hasNoneAtZero)
                {
                    errorMessage = "ItemType에는 value=0 항목이 필요합니다.";
                    return false;
                }
            }
            else
            {
                bool hasMainAtZero = false;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].value == 0)
                    {
                        hasMainAtZero = true;
                        break;
                    }
                }

                if (!hasMainAtZero)
                {
                    errorMessage = "ContainerKind에는 value=0 항목이 필요합니다.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateRoutes(InventoryEnumsDocument document, out string errorMessage)
        {
            errorMessage = null;
            InventoryItemTypeRouteData[] routes = document.itemTypeRoutes ?? Array.Empty<InventoryItemTypeRouteData>();
            var itemTypeValues = new HashSet<int>();
            var containerKindValues = new HashSet<int>();

            for (int i = 0; i < document.itemTypes.Length; i++)
                itemTypeValues.Add(document.itemTypes[i].value);

            for (int i = 0; i < document.containerKinds.Length; i++)
                containerKindValues.Add(document.containerKinds[i].value);

            for (int i = 0; i < routes.Length; i++)
            {
                InventoryItemTypeRouteData route = routes[i];
                if (route == null)
                {
                    errorMessage = $"Route {i + 1}이 비어 있습니다.";
                    return false;
                }

                if (!itemTypeValues.Contains(route.itemTypeValue))
                {
                    errorMessage = $"Route itemTypeValue {route.itemTypeValue}가 정의되지 않았습니다.";
                    return false;
                }

                int[] kinds = route.containerKindValues ?? Array.Empty<int>();
                if (kinds.Length == 0)
                {
                    errorMessage = $"Route itemTypeValue {route.itemTypeValue}에 containerKindValues가 없습니다.";
                    return false;
                }

                for (int j = 0; j < kinds.Length; j++)
                {
                    if (!containerKindValues.Contains(kinds[j]))
                    {
                        errorMessage = $"Route containerKindValue {kinds[j]}가 정의되지 않았습니다.";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
