using PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Example
{
    /// <summary>예제에서 사용할 알림 종류입니다.</summary>
    [NotificationDot]
    public enum ExampleNotification
    {
        Mail = 0,

        [NotificationDot(
            Parent = nameof(Mail),
            ClearOnVisit = true,
            ViewKey = "GreenDot")]
        Inbox = 1,

        [NotificationDot(ViewKey = "GreenDot")]
        Shop = 2,

        [NotificationDot(
            Parent = nameof(Shop),
            ViewKey = "RedDot")]
        FreeItem = 3,

        [NotificationDot(
            Parent = nameof(FreeItem),
            ClearOnVisit = true,
            ViewKey = "RedDot")]
        DailyReward = 4
    }

    [NotificationDot]
    public enum ExampleNotification2
    {

        [NotificationDot(
            ViewKey = "RedDot")]
        FreeItemRRf = 0,
        
    }
}