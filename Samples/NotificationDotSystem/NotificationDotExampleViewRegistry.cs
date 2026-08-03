using PJDev.DevelopKit.Framework.NotificationDotSystem.UI;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Example
{
    /// <summary>예제 알림 키와 프리팹을 코드에서 등록합니다.</summary>
    public sealed class NotificationDotExampleViewRegistry : NotificationDotViewInstaller
    {
        [SerializeField] private GameObject redDot;
        [SerializeField] private GameObject greenDot;

        protected override void RegisterViews()
        {
            Register("RedDot", redDot);
            Register("GreenDot", greenDot);
        }
    }
}