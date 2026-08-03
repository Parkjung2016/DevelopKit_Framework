using System;
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

        private NotificationDotHandle inbox;
        private NotificationDotHandle freeItem;
        private NotificationDotHandle freeItem2;
        private NotificationDotHandle dailyReward;
        private IDisposable subscription;

        private void OnEnable()
        {
            NotificationDots.RegisterEnum<ExampleNotification>();

            inbox = NotificationDots.CreateHandle(ExampleNotification.Inbox, initialInboxCount);
            freeItem = NotificationDots.CreateHandle(ExampleNotification.FreeItem, initialFreeItemCount);
            freeItem2 = NotificationDots.CreateHandle(ExampleNotification2.FreeItemRRf, initialFreeItem2Count);
            dailyReward = NotificationDots.CreateHandle(ExampleNotification.DailyReward, initialDailyRewardCount);

            if (logChanges)
                subscription = NotificationDots.Subscribe<ExampleNotification>(OnChanged);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;

            inbox?.Dispose();
            freeItem?.Dispose();
            dailyReward?.Dispose();
            freeItem2?.Dispose();
            inbox = null;
            freeItem = null;
            dailyReward = null;
            freeItem2 = null;
        }

        [ContextMenu("Test/Add Inbox")]
        public void AddInbox()
        {
            inbox?.Add(1);
        }

        [ContextMenu("Test/Visit Inbox")]
        public void VisitInbox()
        {
            NotificationDots.Visit(ExampleNotification.Inbox);
        }

        [ContextMenu("Test/Add Free Item")]
        public void AddFreeItem()
        {
            freeItem?.Add(1);
        }
        [ContextMenu("Test/Add Free Item2")]
        public void AddFreeItem2()
        {
            freeItem2?.Add(1);
        }
        [ContextMenu("Test/Claim Daily Reward")]
        public void ClaimDailyReward()
        {
            NotificationDots.Visit(ExampleNotification.DailyReward);
        }

        [ContextMenu("Test/Reset Counts")]
        public void ResetCounts()
        {
            inbox?.Clear();
            freeItem?.Clear();
            freeItem2?.Clear();
            dailyReward?.Clear();

            inbox?.SetCount(initialInboxCount);
            freeItem?.SetCount(initialFreeItemCount);
            dailyReward?.SetCount(initialDailyRewardCount);
            freeItem2?.SetCount(initialFreeItem2Count);
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