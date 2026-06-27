namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>뷰를 열 때 런타임으로 적용할 옵션입니다.</summary>
    public readonly struct UIViewOpenOptions
    {
        public static UIViewOpenOptions None => default;

        public readonly object Context;
        public readonly int? Priority;
        public readonly string LayerId;

        public UIViewOpenOptions(object context = null, int? priority = null, string layerId = null)
        {
            Context = context;
            Priority = priority;
            LayerId = layerId;
        }

        public static UIViewOpenOptions WithPriority(int priority, object context = null) =>
            new(context, priority, null);

        public static UIViewOpenOptions WithLayer(string layerId, object context = null, int? priority = null) =>
            new(context, priority, layerId);
    }
}
