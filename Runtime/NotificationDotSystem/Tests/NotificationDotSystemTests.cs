using System;
using NUnit.Framework;
using PJDev.DevelopKit.BasicTemplate.Runtime.PoolSystem;
using PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime;
using PJDev.DevelopKit.Framework.NotificationDotSystem.UI;
using UnityEngine;
using DotSystem = PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime.NotificationDotSystem;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Tests
{
    internal enum TestNotification
    {
        Mail,

        [NotificationDot(Parent = nameof(Mail))]
        Inbox,

        [NotificationDot(Parent = nameof(Mail))]
        Reward
    }

    internal enum OtherNotification
    {
        Source
    }

    internal enum MetadataNotification
    {
        [NotificationDot(ClearOnVisit = true, ViewKey = "RewardBadge")]
        OneShot,

        [NotificationDot(OtherNotification.Source)]
        ActiveDependency,

        [NotificationDot(
            typeof(OtherNotification),
            nameof(OtherNotification.Source),
            NotificationDotDependencyMode.Count)]
        CountDependency
    }
    [NotificationDot]
    internal enum CrossEnumParentNotification
    {
        CrossEnumUniqueReward
    }

    [NotificationDot]
    internal enum CrossEnumChildNotification
    {
        [NotificationDot(Parent = nameof(CrossEnumParentNotification.CrossEnumUniqueReward))]
        FreeItem
    }
    [NotificationDot]
    internal enum TypedParentNotification
    {
        SharedReward
    }

    [NotificationDot]
    internal enum TypedChildNotification
    {
        SharedReward,

        [NotificationDot(
            TypedParentNotification.SharedReward,
            Relation = NotificationDotRelation.Parent)]
        FreeItem
    }
    public sealed class NotificationDotSystemTests
    {
        private DotSystem system;

        [SetUp]
        public void SetUp()
        {
            system = new DotSystem();
        }

        [TearDown]
        public void TearDown()
        {
            NotificationDotViews.Clear();
            NotificationDots.Reset();
            PrefabPool.Clear();
        }

        [Test]
        public void ChildCounts_AreAggregatedIntoParents()
        {
            system.SetCount("Menu/Mail/Inbox", 2);
            system.SetCount("Menu/Mail/Rewards", 3);

            Assert.That(system.GetCount("Menu/Mail"), Is.EqualTo(5));
            Assert.That(system.GetCount("Menu"), Is.EqualTo(5));
            Assert.That(system.GetDirectCount("Menu/Mail"), Is.Zero);
        }

        [Test]
        public void EnumValues_AreAggregatedByEnumType()
        {
            system.SetCount(TestNotification.Inbox, 2);
            system.SetCount(TestNotification.Reward, 3);

            Assert.That(system.GetCount(TestNotification.Inbox), Is.EqualTo(2));
            Assert.That(system.GetCount(TestNotification.Mail), Is.EqualTo(5));
            Assert.That(system.IsActive(TestNotification.Mail), Is.True);
            Assert.That(system.GetDirectCount(TestNotification.Mail), Is.Zero);
            Assert.That(system.GetCount<TestNotification>(), Is.EqualTo(5));
            Assert.That(system.GetCount(NotificationDotEnum.GetKey(TestNotification.Mail)), Is.EqualTo(5));
        }

        [Test]
        public void EnumTypeApi_ProducesTheSameKeys()
        {
            string typeKey = typeof(TestNotification).FullName.Replace('+', '.');
            Assert.That(NotificationDotEnum.GetTypeKey<TestNotification>(), Is.EqualTo(typeKey));
            Assert.That(
                NotificationDotEnum.GetKey(typeof(TestNotification), TestNotification.Inbox),
                Is.EqualTo(NotificationDotEnum.GetKey(TestNotification.Inbox)));
        }

        [Test]
        public void UndefinedEnumValue_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                system.SetCount((TestNotification)999, 1));
        }

        [Test]
        public void ClearOnVisit_HidesCurrentOccurrenceAndCanAppearAgain()
        {
            system.SetActive(MetadataNotification.OneShot, true);

            Assert.That(system.GetViewKey(MetadataNotification.OneShot), Is.EqualTo("RewardBadge"));
            Assert.That(system.Visit(MetadataNotification.OneShot), Is.True);
            Assert.That(system.IsActive(MetadataNotification.OneShot), Is.False);

            system.SetActive(MetadataNotification.OneShot, true);
            Assert.That(system.IsActive(MetadataNotification.OneShot), Is.True);
        }

        [Test]
        public void ClearOnVisit_ShowsOnlyValuesAddedAfterVisit()
        {
            using NotificationDotHandle handle =
                system.CreateHandle(MetadataNotification.OneShot, 3);

            Assert.That(system.Visit(MetadataNotification.OneShot), Is.True);
            Assert.That(system.GetCount(MetadataNotification.OneShot), Is.Zero);

            handle.Add(1);

            Assert.That(system.GetCount(MetadataNotification.OneShot), Is.EqualTo(1));
        }
        [Test]
        public void Dependencies_CanFollowAnotherEnumAsActiveOrCount()
        {
            system.EnsureEnum<MetadataNotification>();
            system.SetCount(OtherNotification.Source, 3);

            Assert.That(system.GetCount(MetadataNotification.ActiveDependency), Is.EqualTo(1));
            Assert.That(system.GetCount(MetadataNotification.CountDependency), Is.EqualTo(3));

            system.Clear(OtherNotification.Source);
            Assert.That(system.IsActive(MetadataNotification.ActiveDependency), Is.False);
            Assert.That(system.IsActive(MetadataNotification.CountDependency), Is.False);
        }

        [Test]
        public void RuntimeDefinition_CanBeInjectedAndRemoved()
        {
            using NotificationDotRegistration registration = system.Register(
                new NotificationDotDefinition("Runtime/Target")
                    .ClearOnVisit()
                    .UseView("RuntimeBadge")
                    .DependsOn("Runtime/Source"));

            system.SetActive("Runtime/Source", true);

            Assert.That(system.IsActive("Runtime/Target"), Is.True);
            Assert.That(system.GetViewKey("Runtime/Target"), Is.EqualTo("RuntimeBadge"));
            Assert.That(system.Visit("Runtime/Target"), Is.True);
            Assert.That(system.IsActive("Runtime/Target"), Is.False);

            system.SetActive("Runtime/Source", false);
            system.SetActive("Runtime/Source", true);
            Assert.That(system.IsActive("Runtime/Target"), Is.True);

            registration.Dispose();
            Assert.That(system.IsActive("Runtime/Target"), Is.False);
            Assert.That(system.TryGetDefinition("Runtime/Target", out _), Is.False);
        }

        [Test]
        public void Handles_KeepContributionsIndependent()
        {
            system.SetCount("Quest/Daily", 2);
            using NotificationDotHandle first = system.CreateHandle("Quest/Daily", 3);
            NotificationDotHandle second = system.CreateHandle("Quest/Daily", 4);

            Assert.That(system.GetCount("Quest/Daily"), Is.EqualTo(9));

            second.Dispose();
            Assert.That(system.GetCount("Quest/Daily"), Is.EqualTo(5));

            first.SetCount(1);
            Assert.That(system.GetCount("Quest/Daily"), Is.EqualTo(3));
        }

        [Test]
        public void CountOverride_ChangesDisplayedValueWithoutChangingHandle()
        {
            using NotificationDotHandle handle = system.CreateHandle("Quest/Daily", 3);

            system.SetCountOverride("Quest/Daily", 1);
            Assert.That(system.GetCount("Quest/Daily"), Is.EqualTo(1));

            system.SetCountOverride("Quest/Daily", 0);
            Assert.That(system.IsActive("Quest/Daily"), Is.False);

            handle.Add(2);
            Assert.That(system.IsActive("Quest/Daily"), Is.False);

            system.ClearCountOverride("Quest/Daily");
            Assert.That(system.GetCount("Quest/Daily"), Is.EqualTo(5));
        }
        [Test]
        public void Batch_NotifiesEachAffectedKeyOnce()
        {
            int calls = 0;
            NotificationDotChange last = default;
            using var subscription = system.Subscribe("Menu", change =>
            {
                calls++;
                last = change;
            }, notifyImmediately: false);

            using (system.BeginBatch())
            {
                system.SetCount("Menu/Mail", 2);
                system.SetCount("Menu/Quest", 3);
            }

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(last.PreviousCount, Is.Zero);
            Assert.That(last.Count, Is.EqualTo(5));
        }

        [Test]
        public void Reset_ClearsValuesAndInvalidatesOldHandles()
        {
            NotificationDotHandle handle = system.CreateHandle("Shop/Free", 3);

            system.Reset();
            handle.SetCount(5);

            Assert.That(system.GetCount("Shop/Free"), Is.Zero);
            handle.Dispose();
            Assert.That(system.GetCount("Shop/Free"), Is.Zero);
        }

        [Test]
        public void TypedParent_UsesExternalEnumWhenNamesOverlap()
        {
            system.SetCount(TypedChildNotification.FreeItem, 3);

            string externalParent = NotificationDotEnum.GetKey(TypedParentNotification.SharedReward);
            string localValue = NotificationDotEnum.GetKey(TypedChildNotification.SharedReward);
            string child = NotificationDotEnum.GetKey(TypedChildNotification.FreeItem);

            Assert.That(child, Is.EqualTo($"{externalParent}/FreeItem"));
            Assert.That(child, Does.Not.StartWith(localValue));
            Assert.That(system.GetCount(TypedParentNotification.SharedReward), Is.EqualTo(3));
            Assert.That(system.GetCount(TypedChildNotification.SharedReward), Is.Zero);
        }
        [Test]
        public void ParentName_FromAnotherEnum_AggregatesAndKeepsChildTypeCount()
        {
            system.SetCount(CrossEnumChildNotification.FreeItem, 2);

            string parentKey = NotificationDotEnum.GetKey(CrossEnumParentNotification.CrossEnumUniqueReward);
            string childKey = NotificationDotEnum.GetKey(CrossEnumChildNotification.FreeItem);
            Assert.That(childKey, Is.EqualTo($"{parentKey}/FreeItem"));
            Assert.That(system.GetCount(CrossEnumParentNotification.CrossEnumUniqueReward), Is.EqualTo(2));
            Assert.That(system.GetCount(CrossEnumChildNotification.FreeItem), Is.EqualTo(2));
            Assert.That(system.GetCount<CrossEnumChildNotification>(), Is.EqualTo(2));
        }
        [Test]
        public void PresenterTarget_UsesEnumTypeAndValue()
        {
            NotificationDotTarget target =
                NotificationDotTarget.Create(TestNotification.Inbox, priority: 25);

            Assert.That(target.EnumType, Is.EqualTo(typeof(TestNotification)));
            Assert.That(target.EnumValue, Is.EqualTo(TestNotification.Inbox));
            Assert.That(target.Key, Is.EqualTo(NotificationDotEnum.GetKey(TestNotification.Inbox)));
            Assert.That(target.DisplayName, Is.EqualTo("TestNotification.Inbox"));
            Assert.That(target.Priority, Is.EqualTo(25));
        }

        [Test]
        public void Presenter_ShowsOnlyHighestPriorityActiveTarget()
        {
            NotificationDots.Reset();
            NotificationDotViews.Clear();

            var root = new GameObject("Presenter");
            var spawnPoint = new GameObject("Spawn Point").transform;
            spawnPoint.SetParent(root.transform);
            var lowPrefab = new GameObject("Low Priority View");
            var highPrefab = new GameObject("High Priority View");
            IDisposable lowRegistration = null;
            IDisposable highRegistration = null;

            try
            {
                lowRegistration = NotificationDotViews.Register(TestNotification.Inbox, lowPrefab);
                highRegistration = NotificationDotViews.Register(TestNotification.Reward, highPrefab);
                NotificationDots.SetActive(TestNotification.Inbox, true);
                NotificationDots.SetActive(TestNotification.Reward, true);

                NotificationDotPresenter presenter = root.AddComponent<NotificationDotPresenter>();
                presenter.SetTargets(new[]
                {
                    NotificationDotTarget.Create(TestNotification.Inbox, priority: 10),
                    NotificationDotTarget.Create(TestNotification.Reward, priority: 100)
                });

                Assert.That(presenter.CurrentKey, Is.EqualTo(NotificationDotEnum.GetKey(TestNotification.Reward)));
                Assert.That(presenter.CurrentCount, Is.EqualTo(1));
                Assert.That(spawnPoint.childCount, Is.EqualTo(1));
            }
            finally
            {
                highRegistration?.Dispose();
                lowRegistration?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(lowPrefab);
                UnityEngine.Object.DestroyImmediate(highPrefab);
                NotificationDotViews.Clear();
                NotificationDots.Reset();
            }
        }
        [Test]
        public void Presenter_ReusesViewAfterItBecomesActiveAgain()
        {
            var root = new GameObject("Presenter");
            var spawnPoint = new GameObject("Spawn Point").transform;
            spawnPoint.SetParent(root.transform);
            var prefab = new GameObject("Notification View");
            IDisposable registration = null;

            try
            {
                registration = NotificationDotViews.Register(TestNotification.Inbox, prefab);
                NotificationDotPresenter presenter = root.AddComponent<NotificationDotPresenter>();
                presenter.SetTargets(new[]
                {
                    NotificationDotTarget.Create(TestNotification.Inbox)
                });

                NotificationDots.SetActive(TestNotification.Inbox, true);
                GameObject firstInstance = spawnPoint.GetChild(0).gameObject;

                NotificationDots.SetActive(TestNotification.Inbox, false);
                Assert.That(spawnPoint.childCount, Is.Zero);
                Assert.That(presenter.HasVisibleDot, Is.False);

                NotificationDots.SetActive(TestNotification.Inbox, true);
                Assert.That(spawnPoint.childCount, Is.EqualTo(1));
                Assert.That(spawnPoint.GetChild(0).gameObject, Is.SameAs(firstInstance));

                Assert.DoesNotThrow(() => root.SetActive(false));
                Assert.That(presenter.HasVisibleDot, Is.False);
                Assert.DoesNotThrow(() => root.SetActive(true));
                Assert.That(spawnPoint.GetChild(0).gameObject, Is.SameAs(firstInstance));
                Assert.That(presenter.HasVisibleDot, Is.True);
            }
            finally
            {
                registration?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Keys_AreNormalized()
        {
            system.SetCount(" /Menu\\Mail//Inbox/ ", 2);

            Assert.That(system.GetCount("Menu/Mail/Inbox"), Is.EqualTo(2));
            Assert.That(system.GetCount("Menu/Mail"), Is.EqualTo(2));
        }
    }
}
