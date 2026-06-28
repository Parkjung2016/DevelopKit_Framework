using System;
using PJDev.DevelopKit.Framework.UISystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal enum UIViewCatalogEntryKind
    {
        Unknown,
        Screen,
        Popup,
        View
    }

    internal static class UIViewCatalogEntryClassifier
    {
        private static readonly UIViewCatalogEntryKind[] DisplayOrder =
        {
            UIViewCatalogEntryKind.Screen,
            UIViewCatalogEntryKind.Popup,
            UIViewCatalogEntryKind.View,
            UIViewCatalogEntryKind.Unknown
        };

        public static ReadOnlySpan<UIViewCatalogEntryKind> DisplayOrderSpan => DisplayOrder;

        public static UIViewCatalogEntryKind Classify(UIViewBase prefab) =>
            Classify(prefab != null ? prefab.GetType() : null);

        public static UIViewCatalogEntryKind Classify(Type viewType)
        {
            if (viewType == null)
                return UIViewCatalogEntryKind.Unknown;

            if (typeof(UIScreenBase).IsAssignableFrom(viewType))
                return UIViewCatalogEntryKind.Screen;

            if (typeof(UIPopupBase).IsAssignableFrom(viewType))
                return UIViewCatalogEntryKind.Popup;

            return UIViewCatalogEntryKind.View;
        }

        public static string GetKindLabel(UIViewCatalogEntryKind kind) =>
            kind switch
            {
                UIViewCatalogEntryKind.Screen => "Screen",
                UIViewCatalogEntryKind.Popup => "Popup",
                UIViewCatalogEntryKind.View => "View",
                _ => "미분류"
            };

        public static Color GetKindColor(UIViewCatalogEntryKind kind) =>
            kind switch
            {
                UIViewCatalogEntryKind.Screen => new Color(0.55f, 0.78f, 1f),
                UIViewCatalogEntryKind.Popup => new Color(0.62f, 0.92f, 0.62f),
                UIViewCatalogEntryKind.View => new Color(0.82f, 0.82f, 0.82f),
                _ => new Color(0.9f, 0.75f, 0.45f)
            };

        public static string GetOpenApiHint(UIViewCatalogEntryKind kind) =>
            kind switch
            {
                UIViewCatalogEntryKind.Screen => "OpenScreen / OpenScreenAsync",
                UIViewCatalogEntryKind.Popup => "OpenPopup / OpenPopupAsync",
                UIViewCatalogEntryKind.View => "OpenPopup / OpenPopupAsync (UIViewBase)",
                _ => "프리팹 지정 후 자동 분류"
            };

        public static bool SupportsMultipleInstances(UIViewCatalogEntryKind kind) =>
            kind is UIViewCatalogEntryKind.Popup or UIViewCatalogEntryKind.View or UIViewCatalogEntryKind.Unknown;

        public static string GetInstancePolicyHint(UIViewCatalogEntryKind kind) =>
            kind switch
            {
                UIViewCatalogEntryKind.Screen =>
                    "Screen은 스택 구조로 viewId당 인스턴스 1개만 사용합니다. 중복 허용은 지원하지 않습니다.",
                UIViewCatalogEntryKind.Popup =>
                    "일반 팝업은 1개 재사용. 토스트는 '중복 허용' + 필요 시 '풀링'으로 재사용합니다.",
                UIViewCatalogEntryKind.View =>
                    "UIViewBase 파생 뷰. Popup과 동일하게 인스턴스 옵션을 설정합니다.",
                _ => "프리팹 또는 viewId를 지정하면 Screen / Popup / View로 분류됩니다."
            };
    }
}
