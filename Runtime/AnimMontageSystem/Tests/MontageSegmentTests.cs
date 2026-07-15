using NUnit.Framework;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Tests
{
    [TestFixture]
    public sealed class MontageSegmentTests
    {
        private MontageTestFixture fixture;

        [SetUp]
        public void SetUp() => fixture = new MontageTestFixture();

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void TrimmedSegment_MapsMontageTimeToClipTime()
        {
            AnimationClip clip = fixture.CreateClip(2f);
            MontageSegment segment = fixture.CreateAnimationSegment(
                clip,
                startTime: 3f,
                clipStartTime: 0.5f,
                clipEndTime: 1.5f,
                playRate: 2f);

            Assert.AreEqual(0.5f, segment.Duration, 0.0001f);
            Assert.AreEqual(1f, segment.ToClipTime(3.25f), 0.0001f);
            Assert.IsTrue(segment.ContainsTime(3.499f));
            Assert.IsFalse(segment.ContainsTime(3.5f));
        }

        [Test]
        public void LoopingSegment_WrapsWithinTrimmedClipRange()
        {
            AnimationClip clip = fixture.CreateClip(2f, true);
            MontageSegment segment = fixture.CreateAnimationSegment(
                clip,
                clipStartTime: 0.5f,
                clipEndTime: 1.5f);

            Assert.IsTrue(segment.IsLoopingClip);
            Assert.AreEqual(0.75f, segment.ToClipTime(1.25f), 0.0001f);
        }
    }
}
