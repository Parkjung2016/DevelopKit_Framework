using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    /// <summary>UIViewCatalog 등록 목록에서 ViewId const C# 코드를 생성합니다.</summary>
    internal static class UIViewIdsScriptGenerator
    {
        private const string GeneratedDirectory =
            "Assets/Framework/Runtime/UISystem/Generated";

        private const string LegacyGeneratedDirectory =
            "Assets/Framework/Runtime/UISystem/Runtime/Generated";

        private const string CodeNamespace = "PJDev.DevelopKit.Framework.UISystem.Runtime";
        private const string RootClassName = "UIViewIds";

        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        private enum ViewIdCategory
        {
            Screen,
            Popup,
            Views
        }

        public static void GenerateWithFeedback(UIViewCatalog catalog)
        {
            if (catalog == null)
            {
                EditorUtility.DisplayDialog(
                    "ViewId 상수 생성",
                    "UIViewCatalog 에셋을 선택하거나 '새 카탈로그'로 만든 뒤 다시 시도하세요.",
                    "확인");
                return;
            }

            if (Generate(catalog, out List<string> outputPaths))
            {
                EditorUtility.DisplayDialog(
                    "ViewId 상수 생성",
                    $"생성 완료:\n{string.Join("\n", outputPaths)}",
                    "확인");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "ViewId 상수 생성",
                    "변경 사항이 없습니다.",
                    "확인");
            }
        }

        public static VisualElement BuildGenerationPanel(UIViewCatalog catalog)
        {
            var panel = UISystemEditorUI.BuildFieldGroup("ViewId 코드 생성");

            panel.Add(UISystemEditorUI.BuildToolbar(
                ("ViewId 상수 생성", () => GenerateWithFeedback(catalog))));

            panel.Add(UISystemEditorUI.BuildHint(
                $"{GeneratedDirectory}에 Screen / Popup / Views 상수를 생성합니다.\n" +
                "예: OpenPopup(UIViewIds.Popup.UI_POPUP), OpenScreen(UIViewIds.Screen.Lobby)"));

            return panel;
        }

        public static bool Generate(UIViewCatalog catalog, out List<string> outputAssetPaths)
        {
            outputAssetPaths = new List<string>();
            if (catalog == null)
                return false;

            Dictionary<ViewIdCategory, Dictionary<string, string>> grouped = CollectViewIdsByCategory(catalog);
            bool changed = false;

            changed |= WriteCategoryFile(ViewIdCategory.Screen, grouped, catalog.name, outputAssetPaths);
            changed |= WriteCategoryFile(ViewIdCategory.Popup, grouped, catalog.name, outputAssetPaths);
            changed |= WriteCategoryFile(ViewIdCategory.Views, grouped, catalog.name, outputAssetPaths);
            changed |= RemoveLegacyFile();

            if (changed)
            {
                UnityEngine.Object[] previousSelection = Selection.objects;
                AssetDatabase.Refresh();
                EditorApplication.delayCall += () => Selection.objects = previousSelection;
            }

            return changed;
        }

        private static bool RemoveLegacyFile()
        {
            bool changed = false;
            changed |= DeleteLegacyAsset(GeneratedDirectory + "/UIViewIds.Generated.cs");
            changed |= DeleteLegacyDirectoryFiles(LegacyGeneratedDirectory);
            return changed;
        }

        private static bool DeleteLegacyAsset(string assetPath)
        {
            if (!File.Exists(Path.GetFullPath(assetPath)))
                return false;

            AssetDatabase.DeleteAsset(assetPath);
            return true;
        }

        private static bool DeleteLegacyDirectoryFiles(string directoryAssetPath)
        {
            string fullDirectory = Path.GetFullPath(directoryAssetPath);
            if (!Directory.Exists(fullDirectory))
                return false;

            bool changed = false;
            foreach (string file in Directory.GetFiles(fullDirectory))
            {
                string assetPath = file.Replace('\\', '/');
                int assetsIndex = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                if (assetsIndex >= 0)
                    assetPath = assetPath.Substring(assetsIndex);

                AssetDatabase.DeleteAsset(assetPath);
                changed = true;
            }

            return changed;
        }

        private static bool WriteCategoryFile(
            ViewIdCategory category,
            Dictionary<ViewIdCategory, Dictionary<string, string>> grouped,
            string catalogName,
            List<string> outputAssetPaths)
        {
            string nestedClassName = GetNestedClassName(category);
            string outputAssetPath = $"{GeneratedDirectory}/{RootClassName}.{nestedClassName}.Generated.cs";
            grouped.TryGetValue(category, out Dictionary<string, string> viewIds);
            viewIds ??= new Dictionary<string, string>(StringComparer.Ordinal);

            string output = BuildSource(viewIds, nestedClassName, category, catalogName);
            string fullPath = Path.GetFullPath(outputAssetPath);
            string outputDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            if (File.Exists(fullPath))
            {
                string existing = File.ReadAllText(fullPath);
                if (string.Equals(existing, output, StringComparison.Ordinal))
                    return false;
            }

            File.WriteAllText(fullPath, output, Encoding.UTF8);
            outputAssetPaths.Add(outputAssetPath);
            return true;
        }

        private static Dictionary<ViewIdCategory, Dictionary<string, string>> CollectViewIdsByCategory(UIViewCatalog catalog)
        {
            var grouped = new Dictionary<ViewIdCategory, Dictionary<string, string>>
            {
                [ViewIdCategory.Screen] = new Dictionary<string, string>(StringComparer.Ordinal),
                [ViewIdCategory.Popup] = new Dictionary<string, string>(StringComparer.Ordinal),
                [ViewIdCategory.Views] = new Dictionary<string, string>(StringComparer.Ordinal)
            };

            IReadOnlyList<UIViewCatalogEntry> entries = catalog.GetEntriesForEditor();
            for (int i = 0; i < entries.Count; i++)
            {
                UIViewCatalogEntry entry = entries[i];
                if (entry == null)
                    continue;

                string viewId = entry.ViewId;
                if (string.IsNullOrEmpty(viewId))
                    continue;

                ViewIdCategory category = Classify(entry.ViewType);
                Dictionary<string, string> bucket = grouped[category];
                if (bucket.ContainsKey(viewId))
                    continue;

                bucket.Add(viewId, entry.ViewType?.Name);
            }

            return grouped;
        }

        private static ViewIdCategory Classify(Type viewType)
        {
            if (viewType == null)
                return ViewIdCategory.Views;

            if (typeof(UIScreenBase).IsAssignableFrom(viewType))
                return ViewIdCategory.Screen;

            if (typeof(UIPopupBase).IsAssignableFrom(viewType))
                return ViewIdCategory.Popup;

            return ViewIdCategory.Views;
        }

        private static string GetNestedClassName(ViewIdCategory category) =>
            category switch
            {
                ViewIdCategory.Screen => "Screen",
                ViewIdCategory.Popup => "Popup",
                _ => "Views"
            };

        private static string GetOpenApiHint(ViewIdCategory category) =>
            category switch
            {
                ViewIdCategory.Screen => "OpenScreen / OpenScreenAsync viewId",
                ViewIdCategory.Popup => "OpenPopup / OpenPopupAsync viewId",
                _ => "Open viewId"
            };

        private static string BuildSource(
            IReadOnlyDictionary<string, string> viewIdsByValue,
            string nestedClassName,
            ViewIdCategory category,
            string catalogName)
        {
            var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);
            var body = new StringBuilder();
            string openApiHint = GetOpenApiHint(category);

            foreach (KeyValuePair<string, string> entry in viewIdsByValue.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                string fieldName = CreateUniqueFieldName(entry.Key, usedFieldNames);
                body.AppendLine($"        /// <summary>{openApiHint}</summary>");
                if (!string.IsNullOrEmpty(entry.Value))
                    body.AppendLine($"        /// <remarks>Prefab type: {entry.Value}</remarks>");

                body.AppendLine($"        public const string {fieldName} = \"{EscapeString(entry.Key)}\";");
                body.AppendLine();
            }

            var source = new StringBuilder();
            source.AppendLine("// <auto-generated />");
            source.AppendLine("// 카탈로그에서 'ViewId 상수 생성' 버튼으로 갱신합니다. 직접 수정하지 마세요.");
            source.AppendLine($"// Source: {catalogName} · {nestedClassName}");
            source.AppendLine();
            source.AppendLine($"namespace {CodeNamespace}");
            source.AppendLine("{");
            source.AppendLine($"    public static partial class {RootClassName}");
            source.AppendLine("    {");
            source.AppendLine($"        public static class {nestedClassName}");
            source.AppendLine("        {");

            if (body.Length == 0)
            {
                source.AppendLine($"            // 등록된 {nestedClassName} 뷰가 없습니다.");
            }
            else
            {
                source.Append(body);
            }

            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        private static string CreateUniqueFieldName(string viewId, HashSet<string> usedFieldNames)
        {
            string baseName = ToMemberIdentifier(viewId);
            string candidate = baseName;
            int suffix = 2;

            while (!usedFieldNames.Add(candidate))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }

            return candidate;
        }

        private static string ToMemberIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "_Empty";

            if (IsValidIdentifier(value) && !CSharpKeywords.Contains(value))
                return value;

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                    builder.Append(c);
            }

            if (builder.Length == 0)
                builder.Append('_');

            if (char.IsDigit(builder[0]))
                builder.Insert(0, '_');

            string identifier = builder.ToString();
            if (CSharpKeywords.Contains(identifier))
                identifier = "@" + identifier;

            return identifier;
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
