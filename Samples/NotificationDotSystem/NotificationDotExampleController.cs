using PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Example
{
    /// <summary>Inspector의 Context Menu나 UI Button으로 알림닷 동작을 확인하는 예제입니다.</summary>
    public sealed class NotificationDotExampleController : MonoBehaviour
    {
        [SerializeField, Min(0)] private int initialInboxCount = 3;
        [SerializeField, Min(0)] private int initialFreeItemCount = 1;
        [SerializeField, Min(0)] private int initialFreeItem2Count = 1;
        [SerializeField, Min(0)] private int initialDailyRewardCount = 1;
        [SerializeField] private bool logChanges = true;

        private void OnEnable()
        {
            ResetCounts();
            if (logChanges)
                NotificationDots.Changed += OnChanged;
        }

        private void OnDisable()
        {
            NotificationDots.Changed -= OnChanged;

            NotificationDots.Clear(ExampleNotification.Inbox);
            NotificationDots.Clear(ExampleNotification.FreeItem);
            NotificationDots.Clear(ExampleNotification2.FreeItemRRf);
            NotificationDots.Clear(ExampleNotification.DailyReward);
        }

        [ContextMenu("Test/Add Inbox")]
        public void AddInbox() => NotificationDots.Add(ExampleNotification.Inbox);

        [ContextMenu("Test/Visit Inbox")]
        public void VisitInbox() => NotificationDots.Visit(ExampleNotification.Inbox);

        [ContextMenu("Test/Add Free Item")]
        public void AddFreeItem() => NotificationDots.Add(ExampleNotification.FreeItem);

        [ContextMenu("Test/Add Free Item2")]
        public void AddFreeItem2() => NotificationDots.Add(ExampleNotification2.FreeItemRRf);

        [ContextMenu("Test/Claim Daily Reward")]
        public void ClaimDailyReward() => NotificationDots.Visit(ExampleNotification.DailyReward);

        [ContextMenu("Test/Reset Counts")]
        public void ResetCounts()
        {
            NotificationDots.SetCount(ExampleNotification.Inbox, initialInboxCount);
            NotificationDots.SetCount(ExampleNotification.FreeItem, initialFreeItemCount);
            NotificationDots.SetCount(ExampleNotification2.FreeItemRRf, initialFreeItem2Count);
            NotificationDots.SetCount(ExampleNotification.DailyReward, initialDailyRewardCount);
        }

        [ContextMenu("Test/Print Current Counts")]
        public void PrintCurrentCounts()
        {
            Debug.Log(
                $"[NotificationDot Example] " +
                $"Mail={NotificationDots.GetCount(ExampleNotification.Mail)}, " +
                $"Inbox={NotificationDots.GetCount(ExampleNotification.Inbox)}, " +
                $"Shop={NotificationDots.GetCount(ExampleNotification.Shop)}, " +
                $"FreeItem={NotificationDots.GetCount(ExampleNotification.FreeItem)}, " +
                $"FreeItem2={NotificationDots.GetCount(ExampleNotification2.FreeItemRRf)}, " +
                $"DailyReward={NotificationDots.GetCount(ExampleNotification.DailyReward)}",
                this);
        }

        private void OnChanged(NotificationDotChange change)
        {
            Debug.Log(
                $"[NotificationDot Example] {change.Key}: " +
                $"{change.PreviousCount} -> {change.Count}",
                this);
        }
    }
}