using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>화면 전환 스택(Screen 레이어)에 올라가는 전체 화면 UI입니다.</summary>
    public class UIScreenBase : UIViewBase
    {
        protected override string ResolveDefaultLayerId() => UILayers.Screen;
    }
}
