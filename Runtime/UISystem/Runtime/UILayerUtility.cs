using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    internal static class UILayerUtility
    {
        public static string GetCanvasGroupId(string layerId, UILayerRegistry registry) =>
            registry != null ? registry.GetCanvasGroupId(layerId) : UICanvasGroups.Floating;

        public static UICanvasGroup GetCanvasGroup(string layerId, UILayerRegistry registry) =>
            UICanvasGroupUtility.IdToEnum(GetCanvasGroupId(layerId, registry));

        public static bool IsInGroup(string layerId, string groupId, UILayerRegistry registry) =>
            string.Equals(GetCanvasGroupId(layerId, registry), groupId, System.StringComparison.Ordinal);

        public static bool IsInGroup(string layerId, UICanvasGroup group, UILayerRegistry registry) =>
            IsInGroup(layerId, UICanvasGroupUtility.EnumToId(group), registry);

        public static void GetLayerIdsInGroup(string groupId, UILayerRegistry registry, List<string> buffer) =>
            registry?.GetLayerIdsInGroup(groupId, buffer);

        public static void GetLayerIdsInGroup(UICanvasGroup group, UILayerRegistry registry, List<string> buffer) =>
            registry?.GetLayerIdsInGroup(group, buffer);
    }
}
