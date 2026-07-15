using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Tests
{
    [TestFixture]
    public sealed class MontageEvaluationTests
    {
        private MontageTestFixture fixture;

        [SetUp]
        public void SetUp() => fixture = new MontageTestFixture();

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void Length_UsesLatestSegmentNotifyOrStateEnd()
        {
            var durationNotify = new TestDurationNotify(1.5f);
            var notify = new AnimNotifyPlacement { Time = 3f, Notify = durationNotify };
            var state = new AnimNotifyStatePlacement
            {
                StartTime = 1f,
                EndTime = 4f,
                NotifyState = new TestNotifyState()
            };
            AnimMontageSO montage = fixture.CreateMontage(
                new[] { fixture.CreateEmptySegment(0f, 2f) },
                new[] { notify },
                new[] { state });

            Assert.AreEqual(4.5f, montage.Length, 0.0001f);
        }

        [Test]
        public void NotifyEvents_FireOnceWhenPlaybackCrossesMarker()
        {
            var first = new AnimNotifyPlacement { Time = 0.5f, Notify = new TestNotify() };
            var second = new AnimNotifyPlacement { Time = 1f, Notify = new TestNotify() };
            AnimMontageSO montage = fixture.CreateMontage(notifies: new[] { first, second });
            var results = new List<AnimNotifyPlacement>();

            MontageEvaluator.CollectNotifyEvents(montage, 0f, 1f, results);

            Assert.AreEqual(2, results.Count);
            Assert.AreSame(first, results[0]);
            Assert.AreSame(second, results[1]);

            MontageEvaluator.CollectNotifyEvents(montage, 1f, 1f, results);
            Assert.AreEqual(1, results.Count);
            Assert.AreSame(second, results[0]);
        }

        [Test]
        public void NotifyDispatcher_DoesNotInvokeSameMarkerTwice()
        {
            var notify = new CountingNotify();
            var placement = new AnimNotifyPlacement { Time = 1f, Notify = notify };
            AnimMontageSO montage = fixture.CreateMontage(
                new[] { fixture.CreateEmptySegment(0f, 1f) },
                new[] { placement });
            var playback = new MontagePlaybackState();
            playback.Begin(montage, 0f);
            playback.Advance(1f);
            var dispatcher = new MontageNotifyDispatcher();
            GameObject owner = fixture.CreateGameObject();

            dispatcher.Dispatch(playback, owner, null, null);
            dispatcher.Dispatch(playback, owner, null, null);

            Assert.AreEqual(1, notify.Count);
        }

        [Test]
        public void NotifyStateTransitions_ReportBeginActiveAndEndBoundaries()
        {
            var placement = new AnimNotifyStatePlacement
            {
                StartTime = 1f,
                EndTime = 2f,
                NotifyState = new TestNotifyState()
            };
            AnimMontageSO montage = fixture.CreateMontage(notifyStates: new[] { placement });
            var begin = new List<AnimNotifyStatePlacement>();
            var end = new List<AnimNotifyStatePlacement>();
            var active = new List<AnimNotifyStatePlacement>();

            MontageEvaluator.CollectNotifyStateTransitions(montage, 0f, 1.5f, begin, end, active);
            Assert.AreEqual(1, begin.Count);
            Assert.AreEqual(0, end.Count);
            Assert.AreEqual(1, active.Count);

            MontageEvaluator.CollectNotifyStateTransitions(montage, 1.5f, 2.5f, begin, end, active);
            Assert.AreEqual(0, begin.Count);
            Assert.AreEqual(1, end.Count);
            Assert.AreEqual(0, active.Count);
        }

        [Test]
        public void SegmentBlending_HoldsPreviousPoseAcrossTimelineGap()
        {
            AnimationClip clip = fixture.CreateClip(1f);
            MontageSegment first = fixture.CreateAnimationSegment(clip);
            MontageSegment second = fixture.CreateAnimationSegment(clip, startTime: 2f);
            var results = new List<MontageSegmentSample>();

            MontageSegmentBlending.Evaluate(1.5f, new[] { first, second }, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(first, results[0].Segment);
            Assert.IsTrue(results[0].IsHeldPose);
            Assert.AreEqual(1f, results[0].Weight, 0.0001f);
        }

        [Test]
        public void SegmentBlending_NormalizesOverlappingSegmentWeights()
        {
            AnimationClip clip = fixture.CreateClip(2f);
            MontageSegment first = fixture.CreateAnimationSegment(clip, blendOut: 0.5f);
            MontageSegment second = fixture.CreateAnimationSegment(
                clip,
                startTime: 1.5f,
                blendIn: 0.5f);
            var results = new List<MontageSegmentSample>();

            MontageSegmentBlending.Evaluate(1.75f, new[] { first, second }, results);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(1f, results[0].Weight + results[1].Weight, 0.0001f);
            Assert.Greater(results[0].Weight, 0f);
            Assert.Greater(results[1].Weight, 0f);
        }

        private sealed class CountingNotify : AnimNotify
        {
            public int Count { get; private set; }

            public override void OnNotify(AnimNotifyContext context) => Count++;
        }

        private sealed class TestNotify : AnimNotify
        {
            public override void OnNotify(AnimNotifyContext context)
            {
            }
        }

        private sealed class TestDurationNotify : AnimNotify, IMontageDurationNotify
        {
            public TestDurationNotify(float duration) => Duration = duration;

            public float Duration { get; }

            public override void OnNotify(AnimNotifyContext context)
            {
            }
        }

        private sealed class TestNotifyState : AnimNotifyState
        {
        }
    }
}
