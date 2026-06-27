using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>프로젝트가 정의하는 Canvas 묶음 한 항목입니다.</summary>
    [Serializable]
    public sealed class UICanvasGroupDefinition
    {
        [SerializeField]
        private string groupId = UICanvasGroups.Floating;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private int sortingOrder = 100;

        [SerializeField]
        private string canvasName;

        public string GroupId => groupId;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? groupId : displayName;

        public int SortingOrder => sortingOrder;

        public string CanvasName => string.IsNullOrEmpty(canvasName) ? $"{DisplayName} Canvas" : canvasName;

        public static UICanvasGroupDefinition Create(
            string id,
            int sortingOrder,
            string display = null,
            string canvas = null)
        {
            return new UICanvasGroupDefinition
            {
                groupId = id,
                displayName = display ?? id,
                sortingOrder = sortingOrder,
                canvasName = canvas
            };
        }
    }
}
