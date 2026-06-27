namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>프레임워크 기본 Canvas 묶음 한 항목입니다.</summary>
    public readonly struct BuiltInCanvasGroupInfo
    {
        public BuiltInCanvasGroupInfo(string groupId, string description, int sortingOrder, string canvasName)
        {
            GroupId = groupId;
            Description = description;
            SortingOrder = sortingOrder;
            CanvasName = canvasName;
        }

        public string GroupId { get; }

        public string Description { get; }

        public int SortingOrder { get; }

        public string CanvasName { get; }

        public UICanvasGroupDefinition ToDefinition() =>
            UICanvasGroupDefinition.Create(GroupId, SortingOrder, GroupId, CanvasName);
    }
}
