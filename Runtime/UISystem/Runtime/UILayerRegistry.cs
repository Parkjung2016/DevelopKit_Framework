using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary><see cref="UILayerSettings"/>를 런타임에 조회하는 레지스트리입니다.</summary>
    public sealed class UILayerRegistry
    {
        private readonly Dictionary<string, UILayerDefinition> definitionsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UICanvasGroupDefinition> canvasGroupsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> layerIdsByGroupId = new(StringComparer.Ordinal);
        private readonly List<string> allLayerIds = new();
        private readonly List<string> allCanvasGroupIds = new();
        private string screenLayerId = UILayers.Screen;
        private int fallbackSortOrder = 200;
        private string fallbackCanvasGroupId = UICanvasGroups.Floating;

        public string ScreenLayerId => screenLayerId;

        public IReadOnlyList<string> AllLayerIds => allLayerIds;

        public IReadOnlyList<string> AllCanvasGroupIds => allCanvasGroupIds;

        public void Initialize(UILayerSettings settings)
        {
            definitionsById.Clear();
            canvasGroupsById.Clear();
            layerIdsByGroupId.Clear();
            allLayerIds.Clear();
            allCanvasGroupIds.Clear();
            screenLayerId = UILayers.Screen;
            fallbackCanvasGroupId = UICanvasGroups.Floating;

            settings?.EnsureDefaults();

            IReadOnlyList<UICanvasGroupDefinition> groupSource = settings != null && settings.CanvasGroups.Count > 0
                ? settings.CanvasGroups
                : UISystemBuiltIn.CreateCanvasGroupDefinitions();

            for (int i = 0; i < groupSource.Count; i++)
            {
                UICanvasGroupDefinition group = groupSource[i];
                if (group == null || string.IsNullOrEmpty(group.GroupId))
                    continue;

                canvasGroupsById[group.GroupId] = group;
                if (!allCanvasGroupIds.Contains(group.GroupId))
                    allCanvasGroupIds.Add(group.GroupId);
            }

            IReadOnlyList<UILayerDefinition> source = settings != null && settings.Layers.Count > 0
                ? settings.Layers
                : UISystemBuiltIn.CreateLayerDefinitions();

            for (int i = 0; i < source.Count; i++)
            {
                UILayerDefinition definition = source[i];
                if (definition == null || string.IsNullOrEmpty(definition.LayerId))
                    continue;

                definitionsById[definition.LayerId] = definition;
                allLayerIds.Add(definition.LayerId);

                string groupId = definition.CanvasGroupId;
                EnsureCanvasGroupExists(groupId);

                if (!layerIdsByGroupId.TryGetValue(groupId, out List<string> groupLayers))
                {
                    groupLayers = new List<string>();
                    layerIdsByGroupId[groupId] = groupLayers;
                }

                groupLayers.Add(definition.LayerId);

                if (definition.UseScreenStack)
                    screenLayerId = definition.LayerId;
            }

            if (definitionsById.TryGetValue(UILayers.Popup, out UILayerDefinition popup))
                fallbackSortOrder = popup.SortOrder;
        }

        public bool TryGet(string layerId, out UILayerDefinition definition) =>
            definitionsById.TryGetValue(layerId, out definition);

        public bool TryGetCanvasGroup(string groupId, out UICanvasGroupDefinition definition) =>
            canvasGroupsById.TryGetValue(groupId, out definition);

        public int GetSortOrder(string layerId)
        {
            if (definitionsById.TryGetValue(layerId, out UILayerDefinition definition))
                return definition.SortOrder;

            return fallbackSortOrder;
        }

        public string GetCanvasGroupId(string layerId)
        {
            if (definitionsById.TryGetValue(layerId, out UILayerDefinition definition))
                return definition.CanvasGroupId;

            return fallbackCanvasGroupId;
        }

        public UICanvasGroup GetCanvasGroup(string layerId) =>
            UICanvasGroupUtility.IdToEnum(GetCanvasGroupId(layerId));

        public bool IsScreenLayer(string layerId) =>
            !string.IsNullOrEmpty(layerId) && string.Equals(layerId, screenLayerId, StringComparison.Ordinal);

        public void GetLayerIdsInGroup(string groupId, List<string> buffer)
        {
            buffer.Clear();
            if (layerIdsByGroupId.TryGetValue(groupId, out List<string> groupLayers))
                buffer.AddRange(groupLayers);
        }

        public void GetLayerIdsInGroup(UICanvasGroup group, List<string> buffer) =>
            GetLayerIdsInGroup(UICanvasGroupUtility.EnumToId(group), buffer);

        private void EnsureCanvasGroupExists(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || canvasGroupsById.ContainsKey(groupId))
                return;

            canvasGroupsById[groupId] = UICanvasGroupDefinition.Create(groupId, 150, groupId);
            allCanvasGroupIds.Add(groupId);
        }
    }
}
