using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>화면 전환 스택(Screen 레이어)에 올라가는 전체 화면 UI입니다.</summary>
    public class UIScreenBase : UIViewBase
    {
        protected override string ResolveDefaultLayerId() => UILayers.Screen;

        protected virtual void OnEnable()
        {
            if (!UIManager.Instance.LayerRegistry.IsScreenLayer(LayerId))
                CDebug.LogWarning($"{name}: UIScreenBase는 Screen 레이어를 권장합니다. 현재={LayerId}");
        }
    }
}
