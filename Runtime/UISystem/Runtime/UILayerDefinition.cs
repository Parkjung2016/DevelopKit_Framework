using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UI 레이어의 ID, 정렬 순서, Canvas 묶음과 루트 이름을 정의합니다.</summary>
    [Serializable]
    public sealed class UILayerDefinition
    {
        [SerializeField] private string layerId = UILayers.Popup;
        [SerializeField] private string displayName;
        [SerializeField] private int sortOrder;
        [SerializeField, HideInInspector] private UICanvasGroup canvasGroup = UICanvasGroup.Floating;
        [UICanvasGroupId]
        [SerializeField] private string canvasGroupId = UICanvasGroups.Floating;
        [SerializeField, HideInInspector] private bool useScreenStack;
        [SerializeField] private string rootName;
        [SerializeField, TextArea(2, 4)] private string description;

        public string LayerId => layerId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? layerId : displayName;
        public string Description => description ?? string.Empty;
        public int SortOrder => sortOrder;
        public string CanvasGroupId => ResolveCanvasGroupId();
        public bool UseScreenStack => useScreenStack;
        public string RootName => string.IsNullOrEmpty(rootName) ? layerId : rootName;

        public static UILayerDefinition Create(
            string id,
            int order,
            string canvasGroupId,
            bool screenStack = false,
            string root = null,
            string descriptionText = null) =>
            new()
            {
                layerId = id,
                displayName = id,
                sortOrder = order,
                canvasGroupId = canvasGroupId,
                canvasGroup = UICanvasGroupUtility.IdToEnum(canvasGroupId),
                useScreenStack = screenStack,
                rootName = root ?? id,
                description = descriptionText ?? string.Empty
            };

        internal void MigrateLegacyCanvasGroup()
        {
            if (string.IsNullOrEmpty(canvasGroupId))
                canvasGroupId = UICanvasGroupUtility.EnumToId(canvasGroup);
        }

        internal void SetUseScreenStack(bool value) => useScreenStack = value;

        private string ResolveCanvasGroupId() =>
            string.IsNullOrEmpty(canvasGroupId)
                ? UICanvasGroupUtility.EnumToId(canvasGroup)
                : canvasGroupId;

        private void OnValidate() => MigrateLegacyCanvasGroup();
    }
}
