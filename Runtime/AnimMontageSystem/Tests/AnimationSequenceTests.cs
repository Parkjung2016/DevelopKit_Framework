using System.Collections.Generic;
using NUnit.Framework;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Tests
{
    [TestFixture]
    public sealed class AnimationSequenceTests
    {
        private MontageTestFixture fixture;

        [SetUp]
        public void SetUp() => fixture = new MontageTestFixture();

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void SetClip_CreatesOneFixedPreviewSegment()
        {
            AnimationClip clip = fixture.CreateClip(1.25f);
            AnimSequenceSO sequence = fixture.CreateSequence(clip);

            Assert.AreEqual(AnimationAssetType.Sequence, sequence.AssetType);
            Assert.AreSame(clip, sequence.Clip);
            Assert.AreEqual(clip.length, sequence.Length, 0.0001f);
            Assert.AreEqual(1, sequence.Segments.Count);
            Assert.AreSame(clip, sequence.Segments[0].Clip);
            Assert.AreEqual(0f, sequence.Segments[0].StartTime, 0.0001f);
            Assert.AreEqual(clip.length, sequence.Segments[0].EndTime, 0.0001f);
        }

        [Test]
        public void Dispatcher_UsesSequenceContextForAnimatorClipNotify()
        {
            AnimationClip clip = fixture.CreateClip(1f);
            AnimSequenceSO sequence = fixture.CreateSequence(clip);
            var notify = new ContextNotify();
            MontageTestFixture.SetField(sequence, "notifies", new[]
            {
                new AnimNotifyPlacement { Time = 0.5f, Notify = notify }
            });
            var dispatcher = new MontageNotifyDispatcher();
            GameObject owner = fixture.CreateGameObject();

            dispatcher.Dispatch(sequence, 0f, 0.75f, owner, null, null, 1f);

            Assert.AreEqual(1, notify.Count);
            Assert.AreSame(sequence, notify.Context.Sequence);
            Assert.AreSame(sequence, notify.Context.AnimationAsset);
            Assert.IsNull(notify.Context.Montage);
        }

        private sealed class ContextNotify : AnimNotify
        {
            public int Count { get; private set; }
            public AnimNotifyContext Context { get; private set; }

            public override void OnNotify(AnimNotifyContext context)
            {
                Count++;
                Context = context;
            }
        }
    }
}
