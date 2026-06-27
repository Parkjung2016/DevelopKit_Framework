using System;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary><see cref="UIViewBase"/>의 layerId 필드용 인스펙터 마커입니다.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UILayerIdAttribute : Attribute
    {
    }
}
