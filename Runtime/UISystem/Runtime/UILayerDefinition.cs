using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>개발자가 정의하는 UI 레이어 한 항목입니다.</summary>
    [Serializable]
    public sealed class UILayerDefinition
    {
        [SerializeField]
        private string layerId = UILayers.Popup;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private int sortOrder;

        [SerializeField, HideInInspector]
        private UICanvasGroup canvasGroup = UICanvasGroup.Floating;

        [UICanvasGroupId]
        [SerializeField]
        private string canvasGroupId = UICanvasGroups.Floating;

        [SerializeField]
        private bool useScreenStack;

        [SerializeField]
        private string rootName;

        public string LayerId => layerId;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? layerId : displayName;

        public int SortOrder => sortOrder;

        /// <summary>이 레이어가 속한 Canvas 묶음 ID입니다.</summary>
        public string CanvasGroupId => ResolveCanvasGroupId();

        /// <summary>레거시 enum 기반 Canvas 묶음입니다.</summary>
        public UICanvasGroup CanvasGroup => UICanvasGroupUtility.IdToEnum(CanvasGroupId);

        public bool UseScreenStack => useScreenStack;

        public string RootName => string.IsNullOrEmpty(rootName) ? layerId : rootName;

        public static UILayerDefinition Create(
            string id,
            int order,
            string canvasGroup,
            bool screenStack = false,
            string root = null)
        {
            return new UILayerDefinition
            {
                layerId = id,
                displayName = id,
                sortOrder = order,
                canvasGroupId = canvasGroup,
                canvasGroup = UICanvasGroupUtility.IdToEnum(canvasGroup),
                useScreenStack = screenStack,
                rootName = root ?? id
            };
        }

        internal void MigrateLegacyCanvasGroup()
        {
            if (!string.IsNullOrEmpty(canvasGroupId))
                return;

            canvasGroupId = UICanvasGroupUtility.EnumToId(canvasGroup);
        }

        private string ResolveCanvasGroupId()
        {
            if (!string.IsNullOrEmpty(canvasGroupId))
                return canvasGroupId;

            return UICanvasGroupUtility.EnumToId(canvasGroup);
        }

        private void OnValidate() => MigrateLegacyCanvasGroup();
    }
}
