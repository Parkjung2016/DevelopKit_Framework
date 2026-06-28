namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>프레임워크 기본 레이어 한 항목입니다. <see cref="UISystemBuiltIn"/>에서 조회합니다.</summary>
    public readonly struct BuiltInLayerInfo
    {
        public BuiltInLayerInfo(
            string layerId,
            string description,
            int sortOrder,
            string canvasGroupId,
            bool useScreenStack,
            string rootName)
        {
            LayerId = layerId;
            Description = description;
            SortOrder = sortOrder;
            CanvasGroupId = canvasGroupId;
            UseScreenStack = useScreenStack;
            RootName = rootName;
        }

        public string LayerId { get; }

        public string Description { get; }

        public int SortOrder { get; }

        public string CanvasGroupId { get; }

        public UICanvasGroup CanvasGroup => UICanvasGroupUtility.IdToEnum(CanvasGroupId);

        public bool UseScreenStack { get; }

        public string RootName { get; }

        public UILayerDefinition ToLayerDefinition() =>
            UILayerDefinition.Create(LayerId, SortOrder, CanvasGroupId, UseScreenStack, RootName, Description);
    }
}
