using NUnit.Framework;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Tests
{
    [TestFixture]
    public sealed class MontagePlaybackStateTests
    {
        private MontageTestFixture fixture;

        [SetUp]
        public void SetUp() => fixture = new MontageTestFixture();

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void Advance_UsesRateScaleAndStopsAtDuration()
        {
            AnimMontageSO montage = fixture.CreateMontage(
                new[] { fixture.CreateEmptySegment(0f, 2f) },
                rateScale: 2f);
            var state = new MontagePlaybackState();

            state.Begin(montage, -1f);
            state.Advance(0.5f);

            Assert.AreEqual(1f, state.CurrentTime, 0.0001f);
            Assert.AreEqual(0.5f, state.NormalizedTime, 0.0001f);
            Assert.IsTrue(state.IsPlaying);

            state.Advance(0.5f);

            Assert.AreEqual(2f, state.CurrentTime, 0.0001f);
            Assert.IsFalse(state.IsPlaying);
        }

        [Test]
        public void PauseAndSetTime_KeepTimeWithinMontageRange()
        {
            AnimMontageSO montage = fixture.CreateMontage(
                new[] { fixture.CreateEmptySegment(0f, 3f) });
            var state = new MontagePlaybackState();
            state.Begin(montage, 1f);

            state.Pause(true);
            state.Advance(0.5f);
            Assert.AreEqual(1f, state.CurrentTime, 0.0001f);

            state.SetTime(10f);
            Assert.AreEqual(3f, state.CurrentTime, 0.0001f);
            Assert.AreEqual(1f, state.PreviousTime, 0.0001f);
        }
    }
}
